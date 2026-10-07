using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Notification;

/// <summary>HO-NTF-001 §12.4 -- <c>channelsConfigured</c> is false for an Unconfigured sender, so
/// the screen can say so (OQ-1).</summary>
public sealed class GetNotificationSummaryQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider, IOptions<NotificationOptions> options)
    : IQueryHandlerAsync<GetNotificationSummaryQuery, NotificationSummaryViewModel>
{
    public async Task<QueryResponse<NotificationSummaryViewModel>> HandleAsync(GetNotificationSummaryQuery query)
    {
        var set = db.Set<NotificationAggregate>().AsNoTracking();

        var queued = await set.CountAsync(n => n.Status == NotificationStatus.Queued);
        var retryPending = await set.CountAsync(n => n.Status == NotificationStatus.RetryPending);
        var sending = await set.CountAsync(n => n.Status == NotificationStatus.Sending);
        var oldestQueuedOn = await set.Where(n => n.Status == NotificationStatus.Queued).OrderBy(n => n.CreatedOn).Select(n => (DateTimeOffset?)n.CreatedOn).FirstOrDefaultAsync();

        var since = timeProvider.GetUtcNow().AddHours(-24);
        var sent = await set.CountAsync(n => n.Status == NotificationStatus.Sent && n.SentOn >= since);
        var undeliverable = await set.CountAsync(n => n.Status == NotificationStatus.Undeliverable && n.ClosedOn >= since);
        var expired = await set.CountAsync(n => n.Status == NotificationStatus.Expired && n.ClosedOn >= since);
        var cancelled = await set.CountAsync(n => n.Status == NotificationStatus.Cancelled && n.ClosedOn >= since);
        var suppressed = await db.Set<NotificationRequestAggregate>().AsNoTracking().CountAsync(r => r.Status == NotificationRequestStatus.Suppressed && r.ProcessedOn >= since);

        var result = new NotificationSummaryViewModel(
            queued, retryPending, sending, oldestQueuedOn,
            new NotificationLast24HoursViewModel(sent, undeliverable, expired, cancelled, suppressed),
            new NotificationChannelsConfiguredViewModel(
                options.Value.Channels.Sms.Sender != "Unconfigured", options.Value.Channels.Email.Sender != "Unconfigured"));

        return new QueryResponse<NotificationSummaryViewModel> { Result = result };
    }
}
