using CatEcommerce.Domain.Aggregates.Reporting;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Reporting;

/// <summary>
/// HO-RPT-001 section 11.7 item 2 (ADR-288, ADR-289) -- resolves a <c>ReportRun</c> attachment: the notification's attachment id is the <b>run</b> id; the run names its HO-FIL-001 stored file; the file
/// store returns the bytes. Read only, <c>AsNoTracking</c>. Returns null when the run is not <c>Generated</c>, has no file, or the file is gone -- the deliver handler then cancels the notification.
/// </summary>
public sealed class ReportRunAttachmentSource(CatEcommerceDbContext db, IFileStore fileStore) : INotificationAttachmentSource
{
    public NotificationAttachmentKind Kind => NotificationAttachmentKind.ReportRun;

    public async Task<NotificationAttachment?> LoadAsync(Guid id, CancellationToken ct = default)
    {
        var run = await db.Set<ScheduledReportRunAggregate>().AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new { r.Status, r.FileId, r.FileName, r.ContentType })
            .FirstOrDefaultAsync(ct);

        if (run is null || run.Status != ScheduledReportRunStatus.Generated || run.FileId is not { } fileId || run.FileName is null || run.ContentType is null) return null;

        var storageKey = await db.StoredFiles.AsNoTracking().Where(f => f.Id == fileId).Select(f => f.StorageKey).FirstOrDefaultAsync(ct);
        if (storageKey is null) return null;

        var opened = await fileStore.OpenReadAsync(storageKey, ct);
        if (!opened.IsSuccess || opened.Value is null) return null;

        await using var stream = opened.Value;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return new NotificationAttachment(run.FileName, run.ContentType, buffer.ToArray());
    }
}
