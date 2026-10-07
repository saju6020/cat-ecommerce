namespace CatEcommerce.Api.RateLimiting;

/// <summary>HO-NTF-001 §12.4/§12.7. Kept separate from HO-ACC-001's <c>RateLimitPolicies</c> (that
/// file is not yet split per area) rather than editing it.</summary>
public static class NotificationRateLimitPolicies
{
    public const string TemplatePreview = "template-preview";
    public const string Unsubscribe = "unsubscribe";
}
