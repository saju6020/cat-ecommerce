using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 8.8 -- cached 60s in memory (Shop:Catalogue:FacetCacheSeconds).</summary>
public sealed class GetCatalogueFiltersQueryHandler(CatEcommerceDbContext db, IMemoryCache cache, Microsoft.Extensions.Options.IOptions<CatEcommerce.Infrastructure.Configuration.ShopOptions> shopOptions)
    : IQueryHandlerAsync<GetCatalogueFiltersQuery, CatalogueFiltersViewModel>
{
    public async Task<QueryResponse<CatalogueFiltersViewModel>> HandleAsync(GetCatalogueFiltersQuery query)
    {
        // Key on the PARSED category, never the raw text: junk input resolves to the all-categories key,
        // so it neither misses the cache nor grows it.
        ProductCategory? parsed = !string.IsNullOrWhiteSpace(query.Category) && EnumerationParsing.TryParse<ProductCategory>(query.Category, out var c) ? c : null;
        var cacheKey = $"shop:filters:{parsed?.Value.ToString() ?? "all"}";
        if (cache.TryGetValue(cacheKey, out CatalogueFiltersViewModel? cached) && cached is not null)
            return new QueryResponse<CatalogueFiltersViewModel> { Result = cached };

        var entries = db.StorefrontCatalogueEntries.AsNoTracking().AsQueryable();

        if (parsed is { } category)
            entries = entries.Where(e => e.Category == category.Value);

        // Anonymous projection, ordered in SQL, THEN mapped: EF cannot order by a member of a record built
        // from a positional constructor (the endpoint answered 500 before this).
        var brandGroups = await entries.GroupBy(e => e.Brand).Select(g => new { Name = g.Key, Count = g.Count() }).OrderBy(b => b.Name).ToListAsync();
        var brands = brandGroups.Select(b => new BrandCount(b.Name, b.Count)).ToList();

        var originGroups = await entries.Where(e => e.CountryOfOriginCode != null)
            .GroupBy(e => e.CountryOfOriginCode!)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToListAsync();
        var origins = originGroups.Select(o => new OriginCount(o.Code, CountryCodes.FindName(o.Code) ?? o.Code, o.Count))
            .OrderBy(o => o.Name).ToList();

        var lifeStageGroups = await entries.Where(e => e.LifeStage != null)
            .GroupBy(e => e.LifeStage!.Value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToListAsync();
        var lifeStages = lifeStageGroups.Select(l => new NamedCount(Enumeration.FromValue<LifeStage>(l.Value).DisplayName, l.Count)).ToList();

        var proteinGroups = await entries.Where(e => e.ProteinSource != null)
            .GroupBy(e => e.ProteinSource!.Value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToListAsync();
        var proteins = proteinGroups.Select(p => new NamedCount(Enumeration.FromValue<ProteinSource>(p.Value).DisplayName, p.Count)).ToList();

        var minPrice = await entries.Where(e => e.MinPrice != null).Select(e => (decimal?)e.MinPrice!.Value).MinAsync();
        var maxPrice = await entries.Where(e => e.MaxPrice != null).Select(e => (decimal?)e.MaxPrice!.Value).MaxAsync();

        var result = new CatalogueFiltersViewModel(brands, origins, lifeStages, proteins, new PriceBounds(minPrice, maxPrice));
        cache.Set(cacheKey, result, TimeSpan.FromSeconds(shopOptions.Value.Catalogue.FacetCacheSeconds));

        return new QueryResponse<CatalogueFiltersViewModel> { Result = result };
    }
}
