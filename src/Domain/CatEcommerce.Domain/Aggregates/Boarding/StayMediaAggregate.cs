using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Boarding;

/// <summary>
/// HO-BRD-001 section 8.17 (US-BRD-12, ADR-228 as renumbered ADR-361) -- a photo or short video of a boarded cat for the owner's stay diary. The bytes are HO-FIL-001's
/// (<see cref="FileId"/>, purpose BoardingMedia); this root holds the reference, caption and date. Its OWN root: it grows with days times cats. Retention is HO-XCT-001's
/// (<c>retention.boardingMediaMonths</c>, D-10): the registered <c>boarding-media</c> purge source erases the stored file and marks the row <see cref="StayMediaStatus.Purged"/>;
/// media attached to an incident are never purged. Staff may hide an item with a reason.
/// </summary>
public sealed class StayMediaAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public Guid StayId { get; private set; }
    public Guid? CatId { get; private set; }
    public Guid? IncidentId { get; private set; }
    public Guid FileId { get; private set; }
    public string? Caption { get; private set; }
    public DateOnly CapturedOnLocalDate { get; private set; }
    public Guid UploadedBy { get; private set; }
    public DateTimeOffset UploadedOn { get; private set; }
    public StayMediaStatus Status { get; private set; } = default!;
    public string? HiddenReason { get; private set; }

    private StayMediaAggregate() { }

    public StayMediaAggregate(Guid id) : base(id) { }

    public void Add(Guid branchId, Guid stayId, Guid? catId, Guid? incidentId, Guid fileId, string? caption, DateOnly capturedOn, DateTimeOffset now, UserContext userContext)
    {
        if (caption is { Length: > IncidentRules.MaxCaptionLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidMediaReference, "The caption is too long.", nameof(caption));
        if (fileId == Guid.Empty)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidMediaReference, "A file is required.", nameof(fileId));

        BranchId = branchId;
        StayId = stayId;
        CatId = catId;
        IncidentId = incidentId;
        FileId = fileId;
        Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        CapturedOnLocalDate = capturedOn;
        UploadedBy = userContext.UserId;
        UploadedOn = now;
        Status = StayMediaStatus.Visible;

        SetDefaultValue(userContext);
        UpdateVersion();
    }

    public void Hide(string reason)
    {
        if (Status != StayMediaStatus.Visible) return;
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > IncidentRules.MaxSkipReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidMediaReference, "A reason is required.", nameof(reason));

        Status = StayMediaStatus.Hidden;
        HiddenReason = reason.Trim();
        UpdateVersion();
    }

    /// <summary>The stored file has been erased by the retention purge source; the row stays as the record that a photo existed.</summary>
    public void MarkPurged()
    {
        if (Status == StayMediaStatus.Purged) return;

        Status = StayMediaStatus.Purged;
        UpdateVersion();
    }
}
