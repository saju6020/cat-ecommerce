using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CatEcommerce.Infrastructure.Files;

/// <summary>
/// HO-FIL-001 §8.7 / HO-XCT-001 §8.9 (ADR-304): "given a purpose and a cut-off, return the files
/// eligible for purge -- excluding <c>LegalHold</c> and any row whose <c>RetainUntil</c> is in the
/// future."
///
/// Gate-fix 2026-09-27: one instance per <see cref="FilePurpose"/> is registered (see
/// <c>FileStorageModule</c>), each with its own <see cref="SourceKey"/>, so a future retention
/// worker can give each purpose its own cutoff rather than one shared cutoff applying to
/// <c>CatProfilePhoto</c> and every clinical/financial purpose alike (CODE_REVIEW/ARCHITECTURE LOW
/// findings). The <c>LegalHold</c>/<c>RetainUntil</c> predicate is applied in the SQL query itself,
/// before <c>Take(batchSize)</c> -- previously applied in memory afterwards, which meant a batch
/// made up mostly of held rows could do no real purging work even when later, genuinely-eligible
/// rows existed beyond it (CODE_REVIEW LOW).
///
/// Gate-fix 2026-09-27 (TEST gate, U11 weak-test finding): the predicate itself is now
/// <see cref="StoredFileAggregate.IsEligibleForPurge"/>, the one shared expression this query and
/// <c>GetFilesForPurgeQueryHandler</c> both pass to EF Core directly, rather than each
/// hand-duplicating the same condition inline -- see that method's own doc comment for why it
/// lives on the aggregate rather than a static bool helper in Shared.
/// </summary>
public sealed class StoredFilePurgeSource(
    FilePurpose purpose,
    CatEcommerceDbContext db,
    IStoredFileEraser eraser,
    TimeProvider timeProvider,
    ILogger<StoredFilePurgeSource> logger) : IRetentionPurgeSource
{
    public string SourceKey => $"stored-file:{purpose.DisplayName}";

    public async Task<PurgeResult> PurgeAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var isEligible = StoredFileAggregate.IsEligibleForPurge(today);

        var pastCutoff = db.StoredFiles.AsNoTracking()
            .Where(f => f.Purpose == purpose && f.UploadedOn <= cutoff);

        // One read for both numbers (FIL backlog row 4): grouping by the eligibility predicate
        // itself returns the eligible and the held count from a single statement, so they can never
        // disagree under concurrent writes, and there is still only one predicate (isEligible) to
        // keep in sync with GetFilesForPurgeQueryHandler's -- not a hand-written negation of it.
        var groups = await pastCutoff
            .GroupBy(isEligible)
            .Select(g => new { Eligible = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var heldCount = groups.Where(g => !g.Eligible).Sum(g => g.Count);

        var eligibleIds = await pastCutoff
            .Where(isEligible)
            .OrderBy(f => f.UploadedOn)
            .Take(batchSize)
            .Select(f => f.Id)
            .ToListAsync(ct);

        int purged = 0, failed = 0;

        foreach (var fileId in eligibleIds)
        {
            try
            {
                // Idempotent (§8.7) -- EraseAsync returning false (row already gone, e.g. a
                // concurrent purge run) is not a failure, just nothing left to do.
                if (await eraser.EraseAsync(fileId, ct)) purged++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to purge stored file {FileId}", fileId);
                failed++;
            }
        }

        return new PurgeResult(eligibleIds.Count + heldCount, purged, heldCount, failed);
    }
}
