namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. A guest cart absorbed into a customer cart is Merged; a guest cart past retention is Abandoned.</summary>
public sealed class CartStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CartStatus Active = new(1, nameof(Active));
    public static readonly CartStatus Merged = new(2, nameof(Merged));
    public static readonly CartStatus Abandoned = new(3, nameof(Abandoned));
}
