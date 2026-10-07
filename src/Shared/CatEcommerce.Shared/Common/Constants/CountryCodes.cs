namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-SHP-001 §8.3/§10.3 -- ISO-3166-1 alpha-2 country codes to their English display name (C-8).
/// Used to validate <c>ProductAggregate.CountryOfOriginCode</c> (<c>InvalidCountryCode</c>) and to
/// label a <c>SourcingOriginAggregate</c> in "Shop by Country" (US-SHP-05). Not exhaustive of every
/// ISO-assigned code, but covers every country a cat-food/litter import business is realistically
/// sourcing from or trading with; a code genuinely missing here is a data addition, not a code
/// change (new countries are appended, never renumbered).
/// </summary>
public static class CountryCodes
{
    private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["BD"] = "Bangladesh",
        ["US"] = "United States",
        ["GB"] = "United Kingdom",
        ["DE"] = "Germany",
        ["FR"] = "France",
        ["IT"] = "Italy",
        ["NL"] = "Netherlands",
        ["BE"] = "Belgium",
        ["ES"] = "Spain",
        ["PT"] = "Portugal",
        ["CH"] = "Switzerland",
        ["AT"] = "Austria",
        ["SE"] = "Sweden",
        ["NO"] = "Norway",
        ["DK"] = "Denmark",
        ["FI"] = "Finland",
        ["IE"] = "Ireland",
        ["PL"] = "Poland",
        ["CZ"] = "Czechia",
        ["HU"] = "Hungary",
        ["GR"] = "Greece",
        ["TH"] = "Thailand",
        ["VN"] = "Vietnam",
        ["MY"] = "Malaysia",
        ["SG"] = "Singapore",
        ["ID"] = "Indonesia",
        ["PH"] = "Philippines",
        ["IN"] = "India",
        ["PK"] = "Pakistan",
        ["LK"] = "Sri Lanka",
        ["CN"] = "China",
        ["JP"] = "Japan",
        ["KR"] = "South Korea",
        ["TW"] = "Taiwan",
        ["AU"] = "Australia",
        ["NZ"] = "New Zealand",
        ["CA"] = "Canada",
        ["MX"] = "Mexico",
        ["BR"] = "Brazil",
        ["AR"] = "Argentina",
        ["CL"] = "Chile",
        ["ZA"] = "South Africa",
        ["EG"] = "Egypt",
        ["AE"] = "United Arab Emirates",
        ["SA"] = "Saudi Arabia",
        ["TR"] = "Turkey",
        ["RU"] = "Russia",
        ["UA"] = "Ukraine",
        ["IL"] = "Israel",
        ["NP"] = "Nepal",
        ["MM"] = "Myanmar",
        ["KH"] = "Cambodia",
        ["LA"] = "Laos",
        ["BT"] = "Bhutan",
        ["MV"] = "Maldives",
        ["HK"] = "Hong Kong",
    };

    /// <summary>Uppercases and looks up. Returns <c>null</c> for an unknown or malformed code.</summary>
    public static string? FindName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 2) return null;
        return Names.TryGetValue(code.ToUpperInvariant(), out var name) ? name : null;
    }

    public static bool IsValid(string? code) => FindName(code) is not null;

    /// <summary>Normalises to upper-case. Caller must have already validated with <see cref="IsValid"/>.</summary>
    public static string Normalise(string code) => code.ToUpperInvariant();

    public static IReadOnlyDictionary<string, string> All => Names;
}
