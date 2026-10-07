using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Notification;

/// <summary>
/// HO-NTF-001 §12.4/§12.6.
///
/// Gate fix 2026-09-27 (ARCHITECTURE HIGH 4 / read-side-pattern rule 1): projects the customisation
/// row with <c>Select</c> instead of <c>Include</c>-ing and materialising the
/// <c>NotificationTemplateAggregate</c> root.
/// </summary>
public sealed class GetNotificationTemplateQueryHandler(CatEcommerceDbContext db, INotificationTemplateCatalogue catalogue)
    : IQueryHandlerAsync<GetNotificationTemplateQuery, NotificationTemplateDetailViewModel?>
{
    public async Task<QueryResponse<NotificationTemplateDetailViewModel?>> HandleAsync(GetNotificationTemplateQuery query)
    {
        var definition = catalogue.Find(query.Key);
        if (definition is null) return new QueryResponse<NotificationTemplateDetailViewModel?> { Result = null };

        var customised = await db.NotificationTemplates.AsNoTracking()
            .Where(t => t.Key == query.Key)
            .Select(t => new
            {
                t.ContentVersion,
                t.LastEditedOn,
                Contents = t.Contents.Select(c => new NotificationTemplateChannelDefault(c.Channel, c.Subject, c.Title, c.Body, c.LinkPath)).ToList()
            })
            .FirstOrDefaultAsync();
        var isCustomised = customised is { } c && c.Contents.Count > 0;
        var customisedContents = (IReadOnlyList<NotificationTemplateChannelDefault>?)customised?.Contents ?? [];
        var customisedContentVersion = customised?.ContentVersion ?? 0;

        var variables = definition.Variables
            .Select(v => new NotificationTemplateVariableViewModel(v.Name, v.Description, v.Kind.DisplayName, v.Sample, v.RequiredOn.Select(r => r.DisplayName).ToList(), false))
            .Concat(StandardTemplateVariables.Common.Select(name => new NotificationTemplateVariableViewModel(
                name, "Standard variable, filled automatically.", StandardTemplateVariables.Kinds[name].DisplayName, "", [], true)))
            .ToList();

        if (definition.Category == NotificationCategory.Marketing)
        {
            variables.Add(new NotificationTemplateVariableViewModel(
                StandardTemplateVariables.UnsubscribeLink, "Unsubscribe link, filled automatically.", TemplateVariableKind.Url.DisplayName,
                "", definition.Channels.Select(c => c.DisplayName).ToList(), true));
        }

        var contents = definition.Channels.Select(channel =>
        {
            var (content, _) = EffectiveNotificationTemplate.ChannelFor(definition, isCustomised ? customisedContents : [], customisedContentVersion, channel);
            return content is null ? null : new NotificationTemplateContentViewModel(channel.DisplayName, content.Subject, content.Title, content.Body, content.LinkPath);
        }).Where(c => c is not null).Select(c => c!).ToList();

        var defaults = definition.Defaults
            .Select(d => new NotificationTemplateContentViewModel(d.Channel.DisplayName, d.Subject, d.Title, d.Body, d.LinkPath))
            .ToList();

        var limits = definition.Channels.Select(BuildLimits).ToList();

        var result = new NotificationTemplateDetailViewModel(
            definition.Key, definition.DisplayName, definition.Description, definition.Category.DisplayName, definition.Urgency.DisplayName,
            definition.Channels.Select(c => c.DisplayName).ToList(), definition.ContainsSecret,
            isCustomised, customisedContentVersion, isCustomised ? customised!.LastEditedOn : null,
            variables, contents, defaults, limits);

        return new QueryResponse<NotificationTemplateDetailViewModel?> { Result = result };
    }

    private static NotificationTemplateChannelLimitsViewModel BuildLimits(NotificationChannel channel)
    {
        if (channel == NotificationChannel.Email) return new NotificationTemplateChannelLimitsViewModel(channel.DisplayName, 150, null, 10_000);
        if (channel == NotificationChannel.Sms) return new NotificationTemplateChannelLimitsViewModel(channel.DisplayName, null, null, 480);
        if (channel == NotificationChannel.InApp) return new NotificationTemplateChannelLimitsViewModel(channel.DisplayName, null, 100, 1000);
        return new NotificationTemplateChannelLimitsViewModel(channel.DisplayName, null, 65, 240);
    }
}
