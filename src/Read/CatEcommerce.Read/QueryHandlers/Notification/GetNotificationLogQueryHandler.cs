using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Notification;

/// <summary>HO-NTF-001 §12.4 -- US-NTF-06 AC1. Destinations masked in the list.</summary>
public sealed class GetNotificationLogQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetNotificationLogQuery, PagedResult<NotificationLogItemViewModel>>
{
    public async Task<QueryResponse<PagedResult<NotificationLogItemViewModel>>> HandleAsync(GetNotificationLogQuery query)
    {
        var filtered = db.Set<NotificationAggregate>().AsNoTracking()
            .Where(n => n.CreatedOn >= query.From && n.CreatedOn <= query.To);

        if (query.RecipientId is { } recipientId) filtered = filtered.Where(n => n.RecipientId == recipientId);
        if (!string.IsNullOrWhiteSpace(query.Destination)) filtered = filtered.Where(n => n.Destination == query.Destination);
        if (!string.IsNullOrWhiteSpace(query.TemplateKey)) filtered = filtered.Where(n => n.TemplateKey == query.TemplateKey);

        if (!string.IsNullOrWhiteSpace(query.Channel) && EnumerationParsing.TryParse<NotificationChannel>(query.Channel, out var channel))
            filtered = filtered.Where(n => n.Channel == channel);

        if (!string.IsNullOrWhiteSpace(query.Status) && EnumerationParsing.TryParse<NotificationStatus>(query.Status, out var status))
            filtered = filtered.Where(n => n.Status == status);

        var totalCount = await filtered.CountAsync();

        var projected =
            from n in filtered
            join s in db.StaffAccounts.AsNoTracking() on n.RecipientId equals s.Id into staffJoin
            from s in staffJoin.DefaultIfEmpty()
            join c in db.CustomerAccounts.AsNoTracking() on n.RecipientId equals c.Id into customerJoin
            from c in customerJoin.DefaultIfEmpty()
            orderby n.CreatedOn descending
            select new
            {
                n.Id, n.CreatedOn, n.TemplateKey, n.Category, n.Channel, n.Destination, n.RecipientKind, n.RecipientId,
                StaffName = s != null ? s.FullName : null, CustomerName = c != null ? c.FullName : null,
                n.Status, n.AttemptCount, n.SentOn, n.NotBefore, n.ResendOfId, n.FallbackFromId
            };

        var rows = await projected.Skip(query.Skip).Take(query.Take).ToListAsync();

        var notificationIds = rows.Select(r => r.Id).ToList();
        var lastResponses = await db.Set<NotificationAggregate>().AsNoTracking()
            .Where(n => notificationIds.Contains(n.Id))
            .Select(n => new { n.Id, LastResponse = n.Attempts.OrderByDescending(a => a.AttemptedOn).Select(a => a.ProviderResponse).FirstOrDefault() })
            .ToListAsync();

        var items = rows.Select(r => new NotificationLogItemViewModel(
            r.Id, r.CreatedOn, r.TemplateKey, ResolveTemplateName(r.TemplateKey), r.Category.DisplayName, r.Channel.DisplayName,
            r.Destination is null ? null : DestinationMask.Mask(r.Destination), r.RecipientKind.DisplayName, r.RecipientId,
            r.StaffName ?? r.CustomerName ?? r.RecipientId.ToString(), r.Status.DisplayName, r.AttemptCount, r.SentOn,
            lastResponses.FirstOrDefault(x => x.Id == r.Id)?.LastResponse, r.NotBefore, r.ResendOfId is not null, r.FallbackFromId is not null))
            .ToList();

        return new QueryResponse<PagedResult<NotificationLogItemViewModel>> { Result = new PagedResult<NotificationLogItemViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }

    private static string ResolveTemplateName(string key) => NotificationTemplateCatalogue.Find(key)?.DisplayName ?? key;
}
