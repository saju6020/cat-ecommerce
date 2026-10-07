using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Notification;

/// <summary>HO-NTF-001 §12.4 -- GET /api/customers/me/inbox. Newest first.</summary>
public sealed class GetInboxQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetInboxQuery, PagedResult<InboxItemViewModel>>
{
    public async Task<QueryResponse<PagedResult<InboxItemViewModel>>> HandleAsync(GetInboxQuery query)
    {
        var filtered = db.Set<NotificationAggregate>().AsNoTracking()
            .Where(n => n.RecipientId == query.CustomerId && n.Channel == NotificationChannel.InApp);

        if (query.UnreadOnly) filtered = filtered.Where(n => n.ReadOn == null);

        var totalCount = await filtered.CountAsync();

        var rows = await filtered
            .OrderByDescending(n => n.CreatedOn)
            .Skip(query.Skip).Take(query.Take)
            .Select(n => new { n.Id, n.Title, n.Body, n.LinkPath, n.Category, n.CreatedOn, n.ReadOn })
            .ToListAsync();

        var items = rows.Select(n => new InboxItemViewModel(n.Id, n.Title, n.Body, n.LinkPath, n.Category.DisplayName, n.CreatedOn, n.ReadOn)).ToList();

        return new QueryResponse<PagedResult<InboxItemViewModel>> { Result = new PagedResult<InboxItemViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}
