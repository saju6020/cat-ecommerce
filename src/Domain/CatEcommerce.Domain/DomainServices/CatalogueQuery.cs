using System.Text;
using System.Text.RegularExpressions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-SHP-001 section 8.8 -- pure. <see cref="Normalise"/> is the one place the search
/// term is trimmed, collapsed, lower-cased and escaped for <c>ILIKE</c>; the read side never
/// repeats this logic.</summary>
public static partial class CatalogueQuery
{
    /// <summary>Trimmed, whitespace-collapsed, lower-cased, capped at
    /// <see cref="ShopSearchRules.MaxQueryLength"/>; <c>%</c>/<c>_</c>/<c>\</c> escaped for
    /// <c>ILIKE</c>.</summary>
    public static string Normalise(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return "";

        var collapsed = CollapseWhitespace().Replace(q.Trim(), " ").ToLowerInvariant();
        if (collapsed.Length > ShopSearchRules.MaxQueryLength) collapsed = collapsed[..ShopSearchRules.MaxQueryLength];

        var escaped = new StringBuilder();
        foreach (var ch in collapsed)
        {
            if (ch is '%' or '_' or '\\') escaped.Append('\\');
            escaped.Append(ch);
        }

        return escaped.ToString();
    }

    /// <summary>Section 8.8: <c>relevance</c> (default when q is present), else <c>newest</c>.</summary>
    public static CatalogueSort DefaultSort(bool hasQuery) => hasQuery ? CatalogueSort.Relevance : CatalogueSort.Newest;

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespace();
}
