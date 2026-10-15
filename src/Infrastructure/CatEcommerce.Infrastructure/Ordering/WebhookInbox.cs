using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>HO-ORD-001 §8.13 -- <see cref="IWebhookInbox"/> over <c>ops.inbound_webhook_receipt</c>: one conflict-ignoring insert on the scoped context's connection
/// (and ambient transaction, if the caller opened one). The unique index <c>ix_inbound_webhook_receipt_provider_event</c> decides a duplicate, even between two
/// concurrent deliveries.</summary>
public sealed class WebhookInbox(CatEcommerceDbContext db) : IWebhookInbox
{
    public async Task<bool> TryRecordAsync(string provider, string eventId, DateTimeOffset receivedOn, CancellationToken ct)
    {
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO ops.inbound_webhook_receipt (id, provider, event_id, received_on) VALUES ({Guid.NewGuid()}, {provider}, {eventId}, {receivedOn}) ON CONFLICT (provider, event_id) DO NOTHING", ct);

        return inserted == 1;
    }
}
