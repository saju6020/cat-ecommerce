namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 §8.4/§10.1 -- ADR-095. Draft -> Active -> Archived, Active -> Draft,
/// Archived -> Active. Any other transition is refused by the aggregate.</summary>
public sealed class ProductStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ProductStatus Draft = new(1, nameof(Draft));
    public static readonly ProductStatus Active = new(2, nameof(Active));
    public static readonly ProductStatus Archived = new(3, nameof(Archived));
}
