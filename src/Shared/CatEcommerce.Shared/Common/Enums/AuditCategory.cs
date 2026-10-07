namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ACC-001 §10.1 / §8.11.</summary>
public sealed class AuditCategory(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AuditCategory DataChange = new(1, nameof(DataChange));
    public static readonly AuditCategory AuthenticationEvent = new(2, nameof(AuthenticationEvent));
    public static readonly AuditCategory BusinessEvent = new(3, nameof(BusinessEvent));
}
