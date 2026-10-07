using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-NTF-001 §8.2 -- what exists is code; what it says is data. Exposed as a port
/// (rather than calling the static <c>NotificationTemplateCatalogue</c> directly) so a test can
/// add a definition (a marketing template, an urgent one) without touching the shipped catalogue.
/// Implemented by <c>StaticNotificationTemplateCatalogue</c> (Infrastructure, Singleton).</summary>
public interface INotificationTemplateCatalogue
{
    IReadOnlyList<NotificationTemplateDefinition> All { get; }
    NotificationTemplateDefinition? Find(string key);
}
