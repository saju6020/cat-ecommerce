using CatEcommerce.Read.QueryHandlers.StoredFile;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Persistence.Views;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>
/// HO-SHP-001 section 8.8 -- one SQL query over <c>product.storefront_catalogue_entry</c>.
/// Filtering, sorting and paging happen entirely in the database; nothing is filtered in memory
/// after materialisation (ADR-083).
/// </summary>
public sealed class GetCatalogueQueryHandler(CatEcommerceDbContext db, IContentLocaleSettings localeSettings, Microsoft.Extensions.Options.IOptions<CatEcommerce.Infrastructure.Configuration.ShopOptions> shopOptions, TimeProvider timeProvider, IStoredFileSigningBatcher fileSigner, ISkuAvailabilityReader availabilityReader) : IQueryHandlerAsync<GetCatalogueQuery, CatalogueResult>
{
    public async Task<QueryResponse<CatalogueResult>> HandleAsync(GetCatalogueQuery query)
    {
        var normalisedQ = CatalogueQuery.Normalise(query.Q);
        var hasQuery = normalisedQ.Length > 0;

        // Availability (HO-SHP-001 section 8.7): the view's is_on_sale (an Active SKU priced now) AND Inventory's inventory.v_sku_sellable
        // figure for the storefront branch -- one predicate for the filter, the page labels and the suggestions.
        var branchId = shopOptions.Value.EffectiveStorefrontBranchId;

        var filtered = ApplyFilters(db, db.StorefrontCatalogueEntries.AsNoTracking(), query, normalisedQ, hasQuery);

        // The availability filter is a semi-join inside the query (after the other filters), not a catalogue-wide GUID list; only the
        // bundles that survived the other filters need the per-SKU component check.
        if (query.AvailabilityInStock == true)
        {
            var now = timeProvider.GetUtcNow();
            var bundleIds = await StorefrontAvailability.SellableBundleProductIdsAsync(
                db, filtered.Where(e => e.Kind == ProductKind.Bundle.Value).Select(e => e.ProductId), now, branchId);
            filtered = StorefrontAvailability.WhereSellable(db, filtered, now, branchId, bundleIds);
        }

        var totalCount = await filtered.CountAsync();

        var sort = ResolveSort(query.Sort, hasQuery);
        var sorted = ApplySort(filtered, sort, normalisedQ);

        // Two steps so the page's rows are priced and joined only for the page: the ids come from the filtered, sorted query (which, unless the
        // sort is by price, reads none of the view's pricing), then the page's entries are read by id and put back in that order.
        var pageIds = await sorted.Skip(query.Skip).Take(query.Take).Select(e => e.ProductId).ToListAsync();
        var rows = await LoadInOrderAsync(pageIds);
        // US-SHP-14: the page's names in the requested language (per-field fallback); the
        // filtering, sorting and paging above never depended on it.
        var sellable = await StorefrontAvailability.SellableProductIdsAsync(db, rows.Select(r => r.ProductId).ToList(), timeProvider.GetUtcNow(), branchId);
        var productItems = await ProductImageUrls.WithPrimaryImageUrlsAsync(db, fileSigner,
            await CatalogueItemLocaliser.ApplyAsync(db, localeSettings, rows.Select(r => ToCatalogueItem(r, sellable.Contains(r.ProductId))).ToList(), query.Locale));
        // One card per size (owner, 2026-10-18): the page's products, each spread into its sizes.
        var items = await CatalogueSizeExpansion.ExpandAsync(db, availabilityReader, fileSigner, productItems, timeProvider.GetUtcNow(),
            dearestFirst: sort == CatalogueSort.PriceDescending, query.MinPrice, query.MaxPrice, inStockOnly: query.AvailabilityInStock == true);
        var filteredIds = filtered.Select(e => e.ProductId);
        var activeSkuCount = await db.SkuRows().CountAsync(s => s.SkuStatus == SkuStatus.Active && filteredIds.Contains(s.ProductId));
        var totalItemCount = Math.Max(totalCount, activeSkuCount);

        CatalogueSuggestions? suggestions = null;
        if (hasQuery && totalCount == 0)
        {
            suggestions = await BuildSuggestionsAsync(normalisedQ, branchId);
            suggestions = suggestions with { Popular = await ProductImageUrls.WithPrimaryImageUrlsAsync(db, fileSigner, await CatalogueItemLocaliser.ApplyAsync(db, localeSettings, suggestions.Popular, query.Locale)) };
        }

        var appliedFilters = BuildAppliedFilters(query, sort);

        var result = new CatalogueResult(items, query.PageNumber, query.PageSize, totalCount, appliedFilters, suggestions, totalItemCount);
        return new QueryResponse<CatalogueResult> { Result = result };
    }

    private static IQueryable<StorefrontCatalogueEntry> ApplyFilters(CatEcommerceDbContext db, IQueryable<StorefrontCatalogueEntry> source, GetCatalogueQuery query, string normalisedQ, bool hasQuery)
    {
        if (hasQuery)
        {
            source = source.Where(e =>
                EF.Functions.ILike(e.SearchText, $"%{normalisedQ}%") ||
                EF.Functions.TrigramsWordSimilarity(normalisedQ, e.SearchText) >= ShopSearchRules.WordSimilarityThreshold);
        }

        if (!string.IsNullOrWhiteSpace(query.Category) && EnumerationParsing.TryParse<ProductCategory>(query.Category, out var category) && category is not null)
            source = source.Where(e => e.Category == category.Value);

        if (!string.IsNullOrWhiteSpace(query.Subcategory) && EnumerationParsing.TryParse<ProductSubcategory>(query.Subcategory, out var subcategory) && subcategory is not null)
            source = source.Where(e => e.Subcategory == subcategory.Value);

        if (query.Brand.Count > 0)
        {
            var brandsLower = query.Brand.Select(b => b.ToLowerInvariant()).ToList();
            source = source.Where(e => brandsLower.Contains(e.Brand.ToLower()));
        }

        if (query.Origin.Count > 0)
        {
            var origins = query.Origin.Where(CountryCodes.IsValid).Select(CountryCodes.Normalise).ToList();
            source = source.Where(e => e.CountryOfOriginCode != null && origins.Contains(e.CountryOfOriginCode));
        }

        if (query.LifeStage.Count > 0)
        {
            var lifeStageValues = query.LifeStage
                .Select(l => EnumerationParsing.TryParse<LifeStage>(l, out var ls) ? ls : null)
                .Where(ls => ls is not null).Select(ls => ls!.Value).ToList();
            var allLifeStagesValue = LifeStage.AllLifeStages.Value;
            source = source.Where(e => e.LifeStage != null && (lifeStageValues.Contains(e.LifeStage.Value) || e.LifeStage.Value == allLifeStagesValue));
        }

        if (query.Protein.Count > 0)
        {
            var proteinValues = query.Protein
                .Select(p => EnumerationParsing.TryParse<ProteinSource>(p, out var ps) ? ps : null)
                .Where(ps => ps is not null).Select(ps => ps!.Value).ToList();
            source = source.Where(e => e.ProteinSource != null && proteinValues.Contains(e.ProteinSource.Value));
        }

        // Section 12.4: a product matches when ANY active SKU's CURRENT price is within [minPrice, maxPrice] --
        // not when its [min, max] range overlaps the bounds, and a product with no current price never
        // matches once a bound is given. Correlated per-SKU current price (the SkuPricing rule).
        // Row 41: "now" is the DATABASE clock (DateTimeOffset.UtcNow translates to now()) -- the same one the
        // view's displayed min/max price use -- never the application clock, so a price flipping at the
        // boundary cannot be filtered one way and displayed the other.
        if (query.MinPrice is not null || query.MaxPrice is not null)
        {
            var low = query.MinPrice;
            var high = query.MaxPrice;
            source = source.Where(e => db.Products
                .Where(p => p.Id == e.ProductId)
                .SelectMany(p => p.Skus.Where(s => s.Status == SkuStatus.Active))
                .Select(s => db.SkuPrices
                    .Where(sp => sp.SkuId == s.Id && sp.EffectiveFrom <= DateTimeOffset.UtcNow)
                    .OrderByDescending(sp => sp.EffectiveFrom)
                    .Select(sp => (decimal?)sp.Amount.Amount)
                    .FirstOrDefault())
                .Any(current => current != null && (low == null || current >= low) && (high == null || current <= high)));
        }

        if (query.MinRating is { } minRating)
            source = source.Where(e => e.RatingAverage != null && e.RatingAverage >= minRating);

        return source;
    }

    private static CatalogueSort ResolveSort(string? sort, bool hasQuery)
    {
        if (!string.IsNullOrWhiteSpace(sort) && EnumerationParsing.TryParse<CatalogueSort>(ToPascalCase(sort), out var parsed) && parsed is not null)
            return parsed;

        return CatalogueQuery.DefaultSort(hasQuery);
    }

    private static string ToPascalCase(string sortParam) => sortParam switch
    {
        "relevance" => "Relevance",
        "newest" => "Newest",
        "price_asc" => "PriceAscending",
        "price_desc" => "PriceDescending",
        "popularity" => "Popularity",
        "rating" => "Rating",
        _ => sortParam
    };

    private static IQueryable<StorefrontCatalogueEntry> ApplySort(IQueryable<StorefrontCatalogueEntry> source, CatalogueSort sort, string normalisedQ)
    {
        IOrderedQueryable<StorefrontCatalogueEntry> ordered;

        if (sort == CatalogueSort.Relevance)
            ordered = source.OrderByDescending(e => EF.Functions.TrigramsWordSimilarity(normalisedQ, e.SearchText));
        else if (sort == CatalogueSort.PriceAscending)
            ordered = source.OrderBy(e => e.MinPrice ?? decimal.MaxValue);
        else if (sort == CatalogueSort.PriceDescending)
            ordered = source.OrderByDescending(e => e.MinPrice ?? decimal.MinValue);
        else if (sort == CatalogueSort.Popularity)
            ordered = source.OrderByDescending(e => e.UnitsSold90d).ThenByDescending(e => e.FirstPublishedOn);
        else if (sort == CatalogueSort.Rating)
            ordered = source.OrderByDescending(e => e.RatingAverage ?? -1).ThenByDescending(e => e.RatingCount);
        else
            ordered = source.OrderByDescending(e => e.FirstPublishedOn); // Newest

        return ordered.ThenBy(e => e.ProductId); // stable paging (section 8.8)
    }

    private async Task<CatalogueSuggestions> BuildSuggestionsAsync(string normalisedQ, Guid branchId)
    {
        var didYouMean = await db.CatalogueTerms.AsNoTracking()
            .Where(t => EF.Functions.TrigramsSimilarity(t.Term, normalisedQ) >= ShopSearchRules.DidYouMeanSimilarity)
            .OrderByDescending(t => EF.Functions.TrigramsSimilarity(t.Term, normalisedQ))
            .Select(t => t.Term)
            .Take(3)
            .ToListAsync();

        // The most-sold sellable products. Standard products and bundles are ranked apart (the standard predicate reads none of the view's
        // pricing, so the catalogue is not priced to find eight products) and merged on the same sort key.
        var standard = await TopSellingAsync(StorefrontAvailability.IsSellableStandard(db, branchId));
        var bundles = await TopSellingAsync(StorefrontAvailability.IsSellableBundle(db, branchId));
        var popularIds = standard.Concat(bundles)
            .OrderByDescending(r => r.UnitsSold90d).ThenByDescending(r => r.FirstPublishedOn is null).ThenByDescending(r => r.FirstPublishedOn)
            .Take(ShopSearchRules.PopularSuggestionCount).Select(r => r.ProductId).ToList();
        var popularRows = await LoadInOrderAsync(popularIds);

        return new CatalogueSuggestions(didYouMean, popularRows.Select(r => ToCatalogueItem(r, inStock: true)).ToList());
    }

    private async Task<List<(Guid ProductId, int UnitsSold90d, DateTimeOffset? FirstPublishedOn)>> TopSellingAsync(System.Linq.Expressions.Expression<Func<StorefrontCatalogueEntry, bool>> predicate)
    {
        var top = await db.StorefrontCatalogueEntries.AsNoTracking()
            .Where(predicate)
            .OrderByDescending(e => e.UnitsSold90d).ThenByDescending(e => e.FirstPublishedOn)
            .Take(ShopSearchRules.PopularSuggestionCount)
            .Select(e => new { e.ProductId, e.UnitsSold90d, e.FirstPublishedOn })
            .ToListAsync();
        return top.Select(t => (t.ProductId, t.UnitsSold90d, t.FirstPublishedOn)).ToList();
    }

    private async Task<List<StorefrontCatalogueEntry>> LoadInOrderAsync(IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0) return [];
        var entries = (await db.StorefrontCatalogueEntries.AsNoTracking().Where(e => ids.Contains(e.ProductId)).ToListAsync())
            .ToDictionary(e => e.ProductId);
        return ids.Where(entries.ContainsKey).Select(id => entries[id]).ToList();
    }

    private static IReadOnlyDictionary<string, string?> BuildAppliedFilters(GetCatalogueQuery query, CatalogueSort sort)
    {
        var applied = new Dictionary<string, string?>
        {
            ["q"] = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q,
            ["category"] = query.Category,
            ["subcategory"] = query.Subcategory,
            ["brand"] = query.Brand.Count > 0 ? string.Join(",", query.Brand) : null,
            ["origin"] = query.Origin.Count > 0 ? string.Join(",", query.Origin) : null,
            ["lifeStage"] = query.LifeStage.Count > 0 ? string.Join(",", query.LifeStage) : null,
            ["protein"] = query.Protein.Count > 0 ? string.Join(",", query.Protein) : null,
            ["minPrice"] = query.MinPrice?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            ["maxPrice"] = query.MaxPrice?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            ["availability"] = query.AvailabilityInStock == true ? "inStock" : null,
            ["minRating"] = query.MinRating?.ToString(),
            ["sort"] = sort.DisplayName,
        };

        return applied;
    }

    internal static CatalogueItem ToCatalogueItem(StorefrontCatalogueEntry e, bool inStock)
    {
        var category = Enumeration.FromValue<ProductCategory>(e.Category);
        var subcategory = e.Subcategory is null ? null : Enumeration.FromValue<ProductSubcategory>(e.Subcategory.Value).DisplayName;
        var kind = Enumeration.FromValue<ProductKind>(e.Kind);
        var lifeStage = e.LifeStage is null ? null : Enumeration.FromValue<LifeStage>(e.LifeStage.Value).DisplayName;
        var country = e.CountryOfOriginCode is null ? null : new CountryRef(e.CountryOfOriginCode, CountryCodes.FindName(e.CountryOfOriginCode) ?? e.CountryOfOriginCode);

        // Row 37: the currency is the one STORED with the price (the view's currency_code); a price with no stored
        // currency is not displayed rather than being given an invented one.
        MoneyViewModel? fromPrice = e.MinPrice is null || e.CurrencyCode is null ? null
            : new MoneyViewModel(e.MinPrice.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), e.CurrencyCode);
        var priceVaries = e.MinPrice is not null && e.MaxPrice is not null && e.MinPrice != e.MaxPrice;

        ProductImageRef? primaryImage = e.PrimaryImageFileId is null ? null : new ProductImageRef(null, e.PrimaryImageAltText ?? "", true, null);

        return new CatalogueItem(
            e.ProductId, e.Slug, e.Name, e.Brand, category.DisplayName, subcategory, kind.DisplayName, country, lifeStage,
            e.PrescriptionRequired, fromPrice, priceVaries, inStock ? "InStock" : "OutOfStock",
            new RatingSummary(e.RatingAverage, e.RatingCount), primaryImage);
    }
}
