namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ADM-001 §10.1.</summary>
public sealed class ClosureKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ClosureKind PublicHoliday = new(1, nameof(PublicHoliday));
    public static readonly ClosureKind Closure = new(2, nameof(Closure));
}
