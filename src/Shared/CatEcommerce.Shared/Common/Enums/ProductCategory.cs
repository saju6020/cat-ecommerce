namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 §8.2/§10.1 -- FR-SHP-1's two-level taxonomy, top level. Code-defined
/// (ADR-078): adding a category is an SRS change (OQ-18), not an admin action.</summary>
public sealed class ProductCategory(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ProductCategory CatFood = new(1, nameof(CatFood));
    public static readonly ProductCategory Litter = new(2, nameof(Litter));
    public static readonly ProductCategory LitterPotsAndAccessories = new(3, nameof(LitterPotsAndAccessories));
    public static readonly ProductCategory BagsAndCarriers = new(4, nameof(BagsAndCarriers));
    public static readonly ProductCategory HealthAndGrooming = new(5, nameof(HealthAndGrooming));
    public static readonly ProductCategory ToysAndFurniture = new(6, nameof(ToysAndFurniture));
}
