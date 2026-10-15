namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class DiagnosticOrderStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DiagnosticOrderStatus Pending = new(1, nameof(Pending));
    public static readonly DiagnosticOrderStatus Resulted = new(2, nameof(Resulted));
    public static readonly DiagnosticOrderStatus Cancelled = new(3, nameof(Cancelled));
}
