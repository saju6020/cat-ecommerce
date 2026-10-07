using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Files;

/// <summary>
/// Gate-fix 2026-09-27 (ARCHITECTURE HIGH, C-ALL-2): Files stores a customer's personal data
/// beyond the id -- <c>UploadedBy</c> and <c>OriginalFileName</c> -- for any file a customer
/// uploads (<c>POST /api/customers/me/files</c>). Redacts both, through
/// <see cref="StoredFileAggregate.RedactUploaderPersonalData"/>, never raw SQL (contract rule 3).
///
/// Mirrors <c>AdministrationPersonalDataEraser</c>'s shape: a row under <c>RetainUntil</c> or
/// <c>LegalHold</c> is <see cref="ErasureOutcome.Held"/>, not erased -- the object and the audit
/// trail of who uploaded it must survive for as long as the owning context's own retention rule
/// says so (e.g. a `LabResult` a vet ordered), even after the uploading customer's account is
/// gone. The stored bytes, the hash and the storage key are never touched -- only the two fields
/// that identify the uploader.
/// </summary>
public sealed class FilesPersonalDataEraser(
    CatEcommerceDbContext db,
    IAggregateRootRepository<StoredFileAggregate> fileRepository) : IPersonalDataEraser
{
    public string ContextKey => "files";

    public async Task<ErasureOutcome> EraseAsync(Guid customerAccountId, DateTimeOffset asOf, UserContext actor, CancellationToken ct)
    {
        var fileIds = await db.StoredFiles
            .AsNoTracking()
            .Where(f => f.UploadedBy == customerAccountId)
            .Select(f => f.Id)
            .ToListAsync(ct);

        if (fileIds.Count == 0) return new ErasureOutcome.Completed();

        var today = DateOnly.FromDateTime(asOf.UtcDateTime);
        DateOnly? earliestHeldUntil = null;

        foreach (var fileId in fileIds)
        {
            var file = await fileRepository.GetByIdAsync(fileId);
            if (file is null || file.UploadedBy != customerAccountId) continue; // already redacted or gone

            if (file.LegalHold || (file.RetainUntil.HasValue && file.RetainUntil.Value > today))
            {
                var heldUntil = file.RetainUntil ?? DateOnly.MaxValue;
                if (earliestHeldUntil is null || heldUntil < earliestHeldUntil) earliestHeldUntil = heldUntil;
                continue;
            }

            file.RedactUploaderPersonalData(actor, Guid.NewGuid());
            await fileRepository.UpdateAsync(file, EventPublishOption.InMemoryPublishOnly);
        }

        return earliestHeldUntil is { } until
            ? new ErasureOutcome.Held(until, "HO-FIL-001 §8.1 LegalHold/RetainUntil")
            : new ErasureOutcome.Completed();
    }
}
