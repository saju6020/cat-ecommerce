namespace CatEcommerce.Domain.Entities;

/// <summary>HO-SHP-001 section 8.14 -- owned by <c>ProductAggregate</c>, one per enabled
/// non-default locale.</summary>
public sealed class ProductTranslation
{
    public string Locale { get; private set; } = default!;
    public string? Name { get; private set; }
    public string? Description { get; private set; }
    public string? IngredientList { get; private set; }
    public string? FeedingGuide { get; private set; }

    private ProductTranslation() { }

    public ProductTranslation(string locale, string? name, string? description, string? ingredientList, string? feedingGuide)
    {
        Locale = locale;
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
        IngredientList = string.IsNullOrWhiteSpace(ingredientList) ? null : ingredientList;
        FeedingGuide = string.IsNullOrWhiteSpace(feedingGuide) ? null : feedingGuide;
    }

    public bool IsEmpty => Name is null && Description is null && IngredientList is null && FeedingGuide is null;
}
