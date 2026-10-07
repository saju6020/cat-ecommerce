using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Aggregates.Product;

/// <summary>
/// HO-SHP-001 section 8.14 / ADR-090 -- product content per enabled non-default locale. The enabled
/// locales are configuration, so the handler passes them in; deciding what is allowed with them is
/// this aggregate's (rule 20). The default locale's text lives in the product's own fields and is
/// not translatable (<c>DefaultLocaleNotTranslatable</c>). Translated names join
/// <c>SearchText</c> (U-A10).
/// </summary>
public sealed partial class ProductAggregate
{
    /// <summary>Sets (replaces) one locale's translation. A translation whose four fields are all
    /// empty removes the locale instead ("an empty translation is removed").</summary>
    public void SetTranslation(
        ProductTranslationInputDto content, string defaultLocale, IReadOnlyList<string> enabledLocales,
        UserContext userContext, Guid correlationId)
    {
        EnsureTranslatableLocale(content.Locale, defaultLocale, enabledLocales);

        ValidateTranslationText(content.Name, ProductRules.NameMaxLength, markup: false, nameof(content.Name));
        ValidateTranslationText(content.Description, ProductRules.DescriptionMaxLength, markup: true, nameof(content.Description));
        ValidateTranslationText(content.IngredientList, ProductRules.IngredientListMaxLength, markup: false, nameof(content.IngredientList));
        ValidateTranslationText(content.FeedingGuide, ProductRules.FeedingGuideMaxLength, markup: true, nameof(content.FeedingGuide));

        var translation = new ProductTranslation(content.Locale, content.Name, content.Description, content.IngredientList, content.FeedingGuide);
        _translations.RemoveAll(t => t.Locale == content.Locale);
        if (!translation.IsEmpty) _translations.Add(translation);

        RefreshSearchText();

        AddDomainEvent(new ProductTranslationChangedEvent { ProductId = Id, Locale = content.Locale, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    /// <summary>Removes one locale's translation (a no-op when there is none).</summary>
    public void RemoveTranslation(string locale, string defaultLocale, IReadOnlyList<string> enabledLocales, UserContext userContext, Guid correlationId)
    {
        EnsureTranslatableLocale(locale, defaultLocale, enabledLocales);

        _translations.RemoveAll(t => t.Locale == locale);
        RefreshSearchText();

        AddDomainEvent(new ProductTranslationChangedEvent { ProductId = Id, Locale = locale, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    private static void EnsureTranslatableLocale(string locale, string defaultLocale, IReadOnlyList<string> enabledLocales)
    {
        if (string.Equals(locale, defaultLocale, StringComparison.Ordinal))
            throw new DomainRuleViolationException(BusinessViolationCodes.DefaultLocaleNotTranslatable,
                "The default language's text is the product's own; it cannot be translated.", nameof(locale));

        if (!enabledLocales.Contains(locale, StringComparer.Ordinal))
            throw new DomainRuleViolationException(BusinessViolationCodes.LocaleNotEnabled,
                $"'{locale}' is not an enabled content language.", nameof(locale));
    }

    private static void ValidateTranslationText(string? value, int maxLength, bool markup, string property)
    {
        if (string.IsNullOrEmpty(value)) return;

        if (value.Length > maxLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidTranslationContent,
                $"{property} cannot exceed {maxLength} characters.", property);

        if (ControlCharacters.ContainsDisallowed(value))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidTranslationContent,
                $"{property} contains a disallowed character.", property);

        if (!markup) return;

        var markupError = LimitedMarkup.Validate(value);
        if (markupError is not null)
            throw new DomainRuleViolationException(markupError, $"{property} failed markup validation.", property);
    }
}
