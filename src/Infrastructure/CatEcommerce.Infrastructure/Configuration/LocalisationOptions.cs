using System.Text.RegularExpressions;
using CatEcommerce.Domain.DomainServices;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>
/// HO-SHP-001 section 8.14/10.2 -- bound from configuration section "Localisation". The first entry
/// of <see cref="ContentLocales"/> is the default locale (the product's own fields); no second
/// locale is enabled by default (SRS Q-12), so the default is just <c>["en"]</c>. Validated on start
/// by <see cref="LocalisationOptionsValidator"/>.
/// </summary>
public sealed class LocalisationOptions
{
    public const string SectionName = "Localisation";

    /// <remarks>Empty by default ON PURPOSE: the configuration binder APPENDS bound items to a list that
    /// already has some, so a non-empty initialiser would turn the committed <c>["en"]</c> into
    /// <c>["en", "en"]</c>. <c>appsettings.json</c> carries the shipped default; a missing section fails
    /// validation loudly at start.</remarks>
    public List<string> ContentLocales { get; set; } = [];
}

/// <summary>At least one locale; each matches <c>^[a-z]{2}(-[A-Z]{2})?$</c>; distinct; at most five
/// (the default plus the four translations a product can hold).</summary>
public sealed partial class LocalisationOptionsValidator : IValidateOptions<LocalisationOptions>
{
    public const int MaxLocales = 5;

    public ValidateOptionsResult Validate(string? name, LocalisationOptions options)
    {
        var locales = options.ContentLocales;

        if (locales is null || locales.Count == 0)
            return ValidateOptionsResult.Fail("Localisation:ContentLocales must list at least one locale (the default).");

        if (locales.Count > MaxLocales)
            return ValidateOptionsResult.Fail($"Localisation:ContentLocales may list at most {MaxLocales} locales.");

        var bad = locales.FirstOrDefault(l => string.IsNullOrWhiteSpace(l) || !LocalePattern().IsMatch(l));
        if (bad is not null)
            return ValidateOptionsResult.Fail($"Localisation:ContentLocales entry '{bad}' is not a valid locale (expected e.g. 'en' or 'pt-BR').");

        if (locales.Distinct(StringComparer.Ordinal).Count() != locales.Count)
            return ValidateOptionsResult.Fail("Localisation:ContentLocales must not repeat a locale.");

        return ValidateOptionsResult.Success;
    }

    [GeneratedRegex("^[a-z]{2}(-[A-Z]{2})?$")]
    private static partial Regex LocalePattern();
}

/// <summary><see cref="IContentLocaleSettings"/> over <see cref="LocalisationOptions"/>.</summary>
public sealed class ConfiguredContentLocales(IOptions<LocalisationOptions> options) : IContentLocaleSettings
{
    public string DefaultLocale => options.Value.ContentLocales[0];

    public IReadOnlyList<string> EnabledLocales => options.Value.ContentLocales;
}
