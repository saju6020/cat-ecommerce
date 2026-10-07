using System.Linq.Expressions;
using CatEcommerce.Domain.Events.StoredFile;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.StoredFile;

/// <summary>
/// HO-FIL-001 §8.1. One root, created by the upload pipeline after the scan passes. Its content
/// fields (<see cref="StorageKey"/>, <see cref="Sha256"/>, <see cref="ContentType"/>,
/// <see cref="SizeInBytes"/>, <see cref="OriginalFileName"/>) are never mutated after
/// <see cref="Register"/> -- a replacement is a new file plus a delete of the old one.
///
/// Gate-fix 2026-09-27 (ARCHITECTURE HIGH, HO-FIL-001-ARCHITECTURE.md second pass): a consuming
/// area asks <see cref="IsEligibleForAttach"/> before referencing this file from its own aggregate
/// -- purpose, uploader and freshness, every input of which is this aggregate's own state
/// (.ai/command-handler-pattern.md §6), so the rule lives here rather than being copied into each
/// consumer's handler. It is deliberately side-effect-free: a second-pass gate found that mutating
/// this aggregate (the former <c>TryAttach</c>) forced "attach a file" into a two-aggregate command
/// with no way to make it atomic, and an ordinary business refusal on the OTHER aggregate (e.g.
/// Cat's own photo-limit check) left the file permanently, uselessly claimed. "One file, one
/// consumer" is instead enforced the same way every other natural-key uniqueness in this solution
/// is -- a database unique index on the consumer's own table (<c>clinic.cat_photo.file_id</c>) --
/// which is also naturally atomic under concurrency, unlike a check-then-set pair across two
/// aggregates. <see cref="RedactUploaderPersonalData"/> (the erasure path) is the one remaining
/// mutator beyond <see cref="Register"/>. See HO-FIL-001.md §1 Amended for the rationale in full.
/// </summary>
public sealed class StoredFileAggregate : AggregateRoot
{
    public FilePurpose Purpose { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeInBytes { get; private set; }
    public string Sha256 { get; private set; } = default!;
    public string OriginalFileName { get; private set; } = default!;

    /// <summary>Server-generated, never derived from the uploaded name -- <c>{purpose}/{yyyy}/{MM}/{id:N}</c> (§8.1).</summary>
    public string StorageKey { get; private set; } = default!;

    public Guid UploadedBy { get; private set; }
    public DateTimeOffset UploadedOn { get; private set; }

    /// <summary>Which scanner passed it (§8.1) -- e.g. "ClamAV 0.103/26991", "DevelopmentNoOp",
    /// "EicarTest". Lets a later signature recall find what was scanned by what.</summary>
    public string ScannerName { get; private set; } = default!;

    public DateOnly? RetainUntil { get; private set; }

    /// <summary>Set only by HO-XCT-001 (not built here); always <c>false</c> in this slice.</summary>
    public bool LegalHold { get; private set; }

    private StoredFileAggregate() { }

    public StoredFileAggregate(Guid id) : base(id) { }

    public void Register(StoredFileRegistrationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.StorageKey))
            throw new DomainRuleViolationException(
                BusinessViolationCodes.InvalidFileName, "A storage key is required.", nameof(dto.StorageKey));

        if (string.IsNullOrWhiteSpace(dto.ContentType))
            throw new DomainRuleViolationException(
                BusinessViolationCodes.FileTypeNotAllowed, "A content type is required.", nameof(dto.ContentType));

        if (dto.SizeInBytes <= 0)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.FileTooLarge, "Size must be greater than zero.", nameof(dto.SizeInBytes));

        if (string.IsNullOrWhiteSpace(dto.Sha256))
            throw new DomainRuleViolationException(
                BusinessViolationCodes.FileRejected, "A content hash is required.", nameof(dto.Sha256));

        // Gate-fix 2026-09-27 (ARCHITECTURE MEDIUM): defence in depth -- the upload pipeline
        // (§8.2 steps 2-3) already checked these against FileRules before this command was even
        // dispatched, but the aggregate can check its own state, so it does (the same precedent
        // CatAggregate.AddPhoto already sets for CatPhotoRules).
        var rule = FileRules.Find(dto.Purpose);
        if (rule is null || !rule.AllowedContentTypes.Contains(dto.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new DomainRuleViolationException(
                BusinessViolationCodes.FileTypeNotAllowed,
                $"'{dto.ContentType}' is not allowed for purpose '{dto.Purpose.DisplayName}'.", nameof(dto.ContentType));

        if (dto.SizeInBytes > rule.MaxSizeInBytes)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.FileTooLarge,
                $"Exceeds the maximum size of {rule.MaxSizeInBytes} bytes for purpose '{dto.Purpose.DisplayName}'.",
                nameof(dto.SizeInBytes));

        Purpose = dto.Purpose;
        ContentType = dto.ContentType;
        SizeInBytes = dto.SizeInBytes;
        Sha256 = dto.Sha256;
        OriginalFileName = dto.OriginalFileName;
        StorageKey = dto.StorageKey;
        UploadedBy = dto.UploadedBy;
        UploadedOn = dto.UploadedOn;
        ScannerName = dto.ScannerName;
        RetainUntil = dto.RetainUntil;
        LegalHold = false;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new StoredFileRegisteredEvent
        {
            FileId = Id,
            Purpose = Purpose.DisplayName,
            UploadedBy = UploadedBy,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    /// <summary>
    /// Gate-fix 2026-09-27 (ARCHITECTURE HIGH, second pass): the attach-eligibility rule a
    /// consuming area (Cat Profiles today; EMR/SHP/BRD when they attach files) needs before it may
    /// reference this file from its own aggregate -- purpose match, uploaded by the same caller,
    /// and uploaded within <paramref name="attachWindow"/> of <paramref name="now"/>. Every input
    /// this method needs is this aggregate's own state, so the rule lives here rather than being
    /// copied into each consumer's handler (.ai/command-handler-pattern.md §6).
    ///
    /// Deliberately side-effect-free -- it does not mark the file "claimed" and there is nothing to
    /// save. "One file, one consumer" is enforced instead by the consumer's own unique database
    /// index on the file id (e.g. <c>clinic.cat_photo</c>'s unique index on <c>file_id</c>), which
    /// is atomic under concurrency in a way a check here plus a separate save on this aggregate
    /// never could be, and which does not require this aggregate to be mutated (and therefore
    /// possibly saved) before the consumer's own aggregate has run its own invariants. One boolean,
    /// deliberately, for every refusal reason -- the caller must not distinguish "wrong purpose"
    /// from "someone else's file" from "too old" in its own response.
    /// </summary>
    public bool IsEligibleForAttach(FilePurpose expectedPurpose, Guid callerUserId, DateTimeOffset now, TimeSpan attachWindow) =>
        Purpose == expectedPurpose && UploadedBy == callerUserId && now - UploadedOn <= attachWindow;

    /// <summary>
    /// Gate-fix 2026-09-27 (TEST gate, U11 weak-test finding): §8.7's purge-eligibility rule --
    /// "excluding <see cref="LegalHold"/> and any row whose <see cref="RetainUntil"/> is in the
    /// future" -- as a single <see cref="Expression{TDelegate}"/> both real consumers,
    /// <c>StoredFilePurgeSource</c> (Infrastructure) and <c>GetFilesForPurgeQueryHandler</c> (Read),
    /// pass directly to EF Core's <c>Where</c>/<c>CountAsync</c>, so the two can never disagree.
    /// Replaces the former <c>Shared.Common.Constants.PurgeEligibility.IsEligible(bool, DateOnly?,
    /// DateOnly)</c> static bool helper, which neither consumer actually called (EF Core cannot
    /// translate an arbitrary static-method call into SQL) -- U11 unit-tested that disconnected
    /// helper while the real, inline-duplicated SQL predicate in each consumer went untested by
    /// anything but a live probe. This lives on the aggregate itself, not in Shared, because it is
    /// an <c>Expression&lt;Func&lt;StoredFileAggregate, bool&gt;&gt;</c> -- Shared has no reference to
    /// Domain types (the dependency runs the other way), so it could never have expressed this as an
    /// EF-translatable predicate over this aggregate's own shape.
    /// </summary>
    public static Expression<Func<StoredFileAggregate, bool>> IsEligibleForPurge(DateOnly today) =>
        f => !f.LegalHold && (f.RetainUntil == null || f.RetainUntil.Value <= today);

    /// <summary>
    /// Gate-fix 2026-09-27 (ARCHITECTURE HIGH, C-ALL-2): the erasure path for a customer's own
    /// uploaded file, called by <c>FilesPersonalDataEraser</c>. Redacts the fields that identify
    /// the uploader and the original file name -- never the stored bytes, the hash or the storage
    /// key, which the retention/erasure machinery (§8.7) still needs to find and delete the object.
    /// The caller (the eraser) is responsible for the <c>LegalHold</c>/<c>RetainUntil</c> decision
    /// of whether this may run yet; this method only performs the redaction once told to.
    /// </summary>
    public void RedactUploaderPersonalData(UserContext actor, Guid correlationId)
    {
        if (UploadedBy == Guid.Empty && OriginalFileName == RedactedFileName) return; // already redacted -- idempotent

        UploadedBy = Guid.Empty;
        OriginalFileName = RedactedFileName;

        SetDefaultValue(actor);
        UpdateVersion();
    }

    private const string RedactedFileName = "[redacted]";
}
