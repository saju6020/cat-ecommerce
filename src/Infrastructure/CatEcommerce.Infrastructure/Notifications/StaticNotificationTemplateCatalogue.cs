using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Infrastructure.Notifications;

/// <summary>HO-NTF-001 §8.2/§8.13 -- Singleton. Wraps the shipped, code-defined catalogue plus any
/// additional definitions a test registers (a marketing template for I-B9, an urgent one for I-C2),
/// so the shipped catalogue itself is never edited for a test.</summary>
public sealed class StaticNotificationTemplateCatalogue : INotificationTemplateCatalogue
{
    private readonly List<NotificationTemplateDefinition> _all;

    public StaticNotificationTemplateCatalogue(IEnumerable<NotificationTemplateDefinition>? additional = null)
    {
        _all = [.. NotificationTemplateCatalogue.Definitions, .. additional ?? []];
    }

    public IReadOnlyList<NotificationTemplateDefinition> All => _all;

    public NotificationTemplateDefinition? Find(string key) => _all.FirstOrDefault(d => d.Key == key);
}
