using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-NTF-001 §8.5 / ADR-058 -- pure, no I/O. Rules R1-R9.</summary>
public static class NotificationRoutingPolicy
{
    public static RoutingPlan Plan(NotificationTemplateDefinition definition, RecipientProfile recipient, NotificationChannel? channelHint)
    {
        // R1
        if (!recipient.IsActive) return new RoutingPlan(null, false, NotificationSuppressionReason.RecipientInactive);

        var candidates = Candidates(definition, recipient, channelHint);
        var isMarketing = definition.Category == NotificationCategory.Marketing;

        // R3/R4
        var permitted = candidates.Where(c => isMarketing ? recipient.MarketingGranted(c) : true).ToList();

        var primary = permitted.FirstOrDefault(); // R5 -- Candidates() is already in priority order.

        // R6
        var inboxCopy = recipient.Kind == NotificationRecipientKind.Customer
            && definition.Channels.Contains(NotificationChannel.InApp)
            && !definition.ContainsSecret
            && (!isMarketing || recipient.MarketingGranted(NotificationChannel.InApp));

        if (primary is null && !inboxCopy) return new RoutingPlan(null, false, NotificationSuppressionReason.NoPermittedChannel); // R7

        return new RoutingPlan(primary, inboxCopy, null);
    }

    public static NotificationChannel? NextFallback(NotificationTemplateDefinition definition, RecipientProfile recipient, IReadOnlyCollection<NotificationChannel> tried)
    {
        var isMarketing = definition.Category == NotificationCategory.Marketing;
        var candidates = Candidates(definition, recipient, channelHint: null);

        return candidates
            .Where(c => !tried.Contains(c))
            .Where(c => !isMarketing || recipient.MarketingGranted(c))
            .Cast<NotificationChannel?>()
            .FirstOrDefault();
    }

    /// <summary>R2/R9 -- external candidates in priority order: the hint (transactional only), the
    /// effective preferred channel, then the other of Sms/Email. Kept only when the definition has
    /// wording for the channel AND the recipient has a destination for it. Push is never a candidate.</summary>
    private static List<NotificationChannel> Candidates(NotificationTemplateDefinition definition, RecipientProfile recipient, NotificationChannel? channelHint)
    {
        var isTransactional = definition.Category == NotificationCategory.Transactional;
        var ordered = new List<NotificationChannel>();

        if (isTransactional && channelHint is not null) ordered.Add(channelHint);
        ordered.Add(recipient.EffectivePreferredChannel);
        ordered.Add(recipient.EffectivePreferredChannel == NotificationChannel.Sms ? NotificationChannel.Email : NotificationChannel.Sms);

        return ordered
            .Distinct()
            .Where(NotificationChannelRules.IsDeliverable)
            .Where(definition.Channels.Contains)
            .Where(c => recipient.DestinationFor(c) is not null)
            .ToList();
    }
}
