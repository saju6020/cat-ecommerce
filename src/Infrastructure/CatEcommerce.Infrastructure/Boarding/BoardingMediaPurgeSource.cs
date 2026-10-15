using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Files;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Boarding;

/// <summary>
/// HO-BRD-001 section 8.17 / HO-XCT-001 section 8.9 (C-BRD-2, D-10) -- the <c>boarding-media</c> purge source. Past the cut-off (<c>retention.boardingMediaMonths</c>, computed by the retention
/// worker) it erases the STORED FILE first, then marks the row <see cref="StayMediaStatus.Purged"/> through the aggregate; the row stays as the record that a photo existed. Media attached to an
/// incident are never purged (they are part of a clinical record, D-8) and are counted as held. Idempotent: a purged row is no longer selected; a file already gone is not a failure.
/// </summary>
public sealed class BoardingMediaPurgeSource(
    CatEcommerceDbContext db, IStoredFileEraser fileEraser, IAggregateRootRepository<StayMediaAggregate> mediaRepository, ILogger<BoardingMediaPurgeSource> logger) : IRetentionPurgeSource
{
    public const string Key = "boarding-media";

    public string SourceKey => Key;

    public async Task<PurgeResult> PurgeAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
    {
        var pastCutoff = db.BoardingStayMedia.AsNoTracking().Where(m => m.Status != StayMediaStatus.Purged && m.UploadedOn <= cutoff);
        var held = await pastCutoff.CountAsync(m => m.IncidentId != null, ct);
        var ids = await pastCutoff.Where(m => m.IncidentId == null).OrderBy(m => m.UploadedOn).Take(batchSize).Select(m => new { m.Id, m.FileId }).ToListAsync(ct);

        int purged = 0, failed = 0;
        foreach (var item in ids)
        {
            try
            {
                await fileEraser.EraseAsync(item.FileId, ct); // false = already gone: still mark the row
                var media = await mediaRepository.GetByIdAsync(item.Id);
                if (media is null) continue;

                media.MarkPurged();
                await mediaRepository.UpdateAsync(media, EventPublishOption.InMemoryPublishOnly);
                purged++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to purge boarding media {MediaId}", item.Id);
                failed++;
            }
        }

        return new PurgeResult(ids.Count + held, purged, held, failed);
    }
}
