using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Read.ViewModels;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Notification;

/// <summary>HO-NTF-001 §12.4/§12.6.</summary>
public sealed class GetNotificationTemplatesQueryHandler(CatEcommerceDbContext db, INotificationTemplateCatalogue catalogue)
    : IQueryHandlerAsync<GetNotificationTemplatesQuery, IReadOnlyList<NotificationTemplateSummaryViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<NotificationTemplateSummaryViewModel>>> HandleAsync(GetNotificationTemplatesQuery query)
    {
        var keys = catalogue.All.Select(d => d.Key).ToList();

        var customised = await db.NotificationTemplates.AsNoTracking()
            .Where(t => keys.Contains(t.Key))
            .Select(t => new { t.Key, t.ContentVersion, t.LastEditedOn, HasContents = t.Contents.Count > 0 })
            .ToListAsync();

        var items = catalogue.All.Select(definition =>
        {
            var row = customised.FirstOrDefault(c => c.Key == definition.Key);
            var isCustomised = row is { HasContents: true };

            return new NotificationTemplateSummaryViewModel(
                definition.Key, definition.DisplayName, definition.Description,
                definition.Category.DisplayName, definition.Urgency.DisplayName,
                definition.Channels.Select(c => c.DisplayName).ToList(), definition.ContainsSecret,
                isCustomised, row?.ContentVersion ?? 0, isCustomised ? row!.LastEditedOn : null);
        }).ToList();

        return new QueryResponse<IReadOnlyList<NotificationTemplateSummaryViewModel>> { Result = items };
    }
}
