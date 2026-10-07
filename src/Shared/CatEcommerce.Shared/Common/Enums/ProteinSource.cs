namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 section 8.5/10.1 (OQ-10) -- a food single, primary protein source.</summary>
public sealed class ProteinSource(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ProteinSource Chicken = new(1, nameof(Chicken));
    public static readonly ProteinSource Turkey = new(2, nameof(Turkey));
    public static readonly ProteinSource Duck = new(3, nameof(Duck));
    public static readonly ProteinSource Beef = new(4, nameof(Beef));
    public static readonly ProteinSource Lamb = new(5, nameof(Lamb));
    public static readonly ProteinSource Fish = new(6, nameof(Fish));
    public static readonly ProteinSource Tuna = new(7, nameof(Tuna));
    public static readonly ProteinSource Salmon = new(8, nameof(Salmon));
    public static readonly ProteinSource Rabbit = new(9, nameof(Rabbit));
    public static readonly ProteinSource Insect = new(10, nameof(Insect));
    public static readonly ProteinSource PlantBased = new(11, nameof(PlantBased));
    public static readonly ProteinSource Mixed = new(12, nameof(Mixed));
    public static readonly ProteinSource Other = new(13, nameof(Other));
}
