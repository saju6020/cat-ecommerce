namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 §8.2/§10.1 -- FR-SHP-1's two-level taxonomy, second level. Each member
/// carries its own <see cref="Category"/> parent; a product's subcategory must belong to its own
/// category (<c>InvalidProductCategory</c>).</summary>
public sealed class ProductSubcategory(int value, string displayName, ProductCategory category) : Enumeration(value, displayName)
{
    public ProductCategory Category { get; } = category;

    public static readonly ProductSubcategory Dry = new(1, nameof(Dry), ProductCategory.CatFood);
    public static readonly ProductSubcategory Wet = new(2, nameof(Wet), ProductCategory.CatFood);
    public static readonly ProductSubcategory Treats = new(3, nameof(Treats), ProductCategory.CatFood);
    public static readonly ProductSubcategory Kitten = new(4, nameof(Kitten), ProductCategory.CatFood);
    public static readonly ProductSubcategory Adult = new(5, nameof(Adult), ProductCategory.CatFood);
    public static readonly ProductSubcategory Senior = new(6, nameof(Senior), ProductCategory.CatFood);
    public static readonly ProductSubcategory Therapeutic = new(7, nameof(Therapeutic), ProductCategory.CatFood);

    public static readonly ProductSubcategory Clumping = new(8, nameof(Clumping), ProductCategory.Litter);
    public static readonly ProductSubcategory Silica = new(9, nameof(Silica), ProductCategory.Litter);
    public static readonly ProductSubcategory Tofu = new(10, nameof(Tofu), ProductCategory.Litter);
    public static readonly ProductSubcategory Bentonite = new(11, nameof(Bentonite), ProductCategory.Litter);

    public static readonly ProductSubcategory Trays = new(12, nameof(Trays), ProductCategory.LitterPotsAndAccessories);
    public static readonly ProductSubcategory HoodedBoxes = new(13, nameof(HoodedBoxes), ProductCategory.LitterPotsAndAccessories);
    public static readonly ProductSubcategory Scoops = new(14, nameof(Scoops), ProductCategory.LitterPotsAndAccessories);
    public static readonly ProductSubcategory Mats = new(15, nameof(Mats), ProductCategory.LitterPotsAndAccessories);
    public static readonly ProductSubcategory Liners = new(16, nameof(Liners), ProductCategory.LitterPotsAndAccessories);

    public static readonly ProductSubcategory Carriers = new(17, nameof(Carriers), ProductCategory.BagsAndCarriers);
    public static readonly ProductSubcategory Backpacks = new(18, nameof(Backpacks), ProductCategory.BagsAndCarriers);
    public static readonly ProductSubcategory TravelBags = new(19, nameof(TravelBags), ProductCategory.BagsAndCarriers);
    public static readonly ProductSubcategory StorageBags = new(20, nameof(StorageBags), ProductCategory.BagsAndCarriers);
}
