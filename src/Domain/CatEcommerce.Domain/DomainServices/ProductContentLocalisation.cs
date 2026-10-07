using CatEcommerce.Domain.Entities;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>A product's text in one resolved language, with the language each field actually came
/// from (so the client can mark each field's language).</summary>
public sealed record LocalisedProductContent(
    string Name,
    string Description,
    string? IngredientList,
    string? FeedingGuide,
    string Locale,
    IReadOnlyDictionary<string, string> FieldLocales);

/// <summary>
/// HO-SHP-001 section 8.14 / ADR-090 -- pure. Per field, the requested locale's value when it is
/// present and non-empty, else the default language's (US-SHP-14 AC2: "never an empty field").
/// <c>FieldLocales</c> names, for <c>name</c>, <c>description</c>, <c>ingredientList</c> and
/// <c>feedingGuide</c>, the locale the field actually came from. A requested locale that is unknown,
/// disabled or the default itself resolves to the default language and is never an error on a
/// public read.
/// </summary>
public static class ProductContentLocalisation
{
    public static LocalisedProductContent Resolve(
        string defaultName, string defaultDescription, string? defaultIngredientList, string? defaultFeedingGuide,
        IReadOnlyList<ProductTranslation> translations, string? requestedLocale,
        string defaultLocale, IReadOnlyList<string> enabledLocales)
    {
        var requested = string.IsNullOrWhiteSpace(requestedLocale) ? null : requestedLocale.Trim();
        var usable = requested is not null
            && !string.Equals(requested, defaultLocale, StringComparison.Ordinal)
            && enabledLocales.Contains(requested, StringComparer.Ordinal);

        var translation = usable ? translations.FirstOrDefault(t => string.Equals(t.Locale, requested, StringComparison.Ordinal)) : null;
        var effectiveLocale = usable ? requested! : defaultLocale;

        var fieldLocales = new Dictionary<string, string>
        {
            ["name"] = translation?.Name is { Length: > 0 } ? effectiveLocale : defaultLocale,
            ["description"] = translation?.Description is { Length: > 0 } ? effectiveLocale : defaultLocale,
            ["ingredientList"] = translation?.IngredientList is { Length: > 0 } ? effectiveLocale : defaultLocale,
            ["feedingGuide"] = translation?.FeedingGuide is { Length: > 0 } ? effectiveLocale : defaultLocale,
        };

        return new LocalisedProductContent(
            translation?.Name is { Length: > 0 } name ? name : defaultName,
            translation?.Description is { Length: > 0 } description ? description : defaultDescription,
            translation?.IngredientList is { Length: > 0 } ingredients ? ingredients : defaultIngredientList,
            translation?.FeedingGuide is { Length: > 0 } feeding ? feeding : defaultFeedingGuide,
            effectiveLocale,
            fieldLocales);
    }
}
