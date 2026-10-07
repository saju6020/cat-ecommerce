namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SHP-001 section 8.14 -- the enabled content locales (<c>Localisation:ContentLocales</c>). The
/// first entry is the default locale, whose text lives in the product's own fields. A Domain port
/// implemented in Infrastructure over <c>IOptions&lt;LocalisationOptions&gt;</c> (section 12.2),
/// because command handlers (Application) cannot reference Infrastructure's options type
/// (HO-SHP-001.md section 1 Amended, 2026-10-04).
/// </summary>
public interface IContentLocaleSettings
{
    string DefaultLocale { get; }

    /// <summary>Every enabled locale, the default first.</summary>
    IReadOnlyList<string> EnabledLocales { get; }
}
