using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Notification;

/// <summary>HO-NTF-001 §12.4 -- GET /api/customers/me/inbox/unread-count.</summary>
public sealed class GetInboxUnreadCountQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetInboxUnreadCountQuery, int>
{
    public async Task<QueryResponse<int>> HandleAsync(GetInboxUnreadCountQuery query)
    {
        var count = await db.Set<NotificationAggregate>().AsNoTracking()
            .CountAsync(n => n.RecipientId == query.CustomerId && n.Channel == NotificationChannel.InApp && n.ReadOn == null);

        return new QueryResponse<int> { Result = count };
    }
}
