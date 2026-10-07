using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-NTF-001 §8.2 -- "Effective template = the aggregate's contents if customised, else
/// the definition's defaults (ContentVersion = 0)." Pure, no I/O; shared by C4/C5 and the read-side
/// template queries so they cannot disagree.</summary>
public static class EffectiveNotificationTemplate
{
    public static (NotificationTemplateChannelDefault? Content, int Version) ChannelFor(
        NotificationTemplateDefinition definition, NotificationTemplateAggregate? customised, Shared.Common.Enums.NotificationChannel channel)
    {
        if (customised is not null && customised.Contents.Count > 0)
        {
            var content = customised.Contents.FirstOrDefault(c => c.Channel == channel);
            return content is null
                ? (null, customised.ContentVersion)
                : (new NotificationTemplateChannelDefault(content.Channel, content.Subject, content.Title, content.Body, content.LinkPath), customised.ContentVersion);
        }

        return (definition.DefaultFor(channel), 0);
    }

    /// <summary>
    /// Gate fix 2026-09-27 (ARCHITECTURE HIGH 4 / read-side-pattern rule 1). Same rule as the
    /// overload above, but over already-projected content rather than a materialised
    /// <see cref="NotificationTemplateAggregate"/> -- for the read side, which must not rehydrate
    /// the aggregate root (<c>GetNotificationTemplateQueryHandler</c> projects with <c>Select</c>).
    /// </summary>
    public static (NotificationTemplateChannelDefault? Content, int Version) ChannelFor(
        NotificationTemplateDefinition definition,
        IReadOnlyList<NotificationTemplateChannelDefault> customisedContents, int customisedContentVersion,
        Shared.Common.Enums.NotificationChannel channel)
    {
        if (customisedContents.Count > 0)
        {
            var content = customisedContents.FirstOrDefault(c => c.Channel == channel);
            return (content, customisedContentVersion);
        }

        return (definition.DefaultFor(channel), 0);
    }
}
