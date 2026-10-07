using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>
/// HO-SHP-001 section 8.14 / US-SHP-14 -- substitutes the requested locale's product NAME into a
/// page of catalogue items (a list shows names only). One read for the page's ids; the requested
/// locale is used only when it is enabled and not the default, and a missing or empty translation
/// keeps the default-language name (per-field fallback). Never filters or sorts.
/// </summary>
internal static class CatalogueItemLocaliser
{
    public static async Task<IReadOnlyList<CatalogueItem>> ApplyAsync(
        CatEcommerceDbContext db, IContentLocaleSettings localeSettings, IReadOnlyList<CatalogueItem> items, string? requestedLocale)
    {
        var locale = Usable(localeSettings, requestedLocale);
        if (locale is null || items.Count == 0) return items;

        var ids = items.Select(i => i.ProductId).ToList();
        var names = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, Name = p.Translations.Where(t => t.Locale == locale).Select(t => t.Name).FirstOrDefault() })
            .ToListAsync();

        var byId = names.Where(n => !string.IsNullOrEmpty(n.Name)).ToDictionary(n => n.Id, n => n.Name!);
        return items.Select(i => byId.TryGetValue(i.ProductId, out var name) ? i with { Name = name } : i).ToList();
    }

    /// <summary>The locale to substitute, or null for the default language.</summary>
    public static string? Usable(IContentLocaleSettings localeSettings, string? requestedLocale)
    {
        var requested = string.IsNullOrWhiteSpace(requestedLocale) ? null : requestedLocale.Trim();
        if (requested is null || requested == localeSettings.DefaultLocale) return null;

        return localeSettings.EnabledLocales.Contains(requested, StringComparer.Ordinal) ? requested : null;
    }
}
