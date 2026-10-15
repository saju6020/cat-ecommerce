namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class VisitStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly VisitStatus Open = new(1, nameof(Open));
    public static readonly VisitStatus Finalised = new(2, nameof(Finalised));
}
