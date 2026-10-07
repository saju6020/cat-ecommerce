using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 8.2/12.4 -- US-SHP-01 AC3.</summary>
public sealed class GetCategoriesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCategoriesQuery, IReadOnlyList<CategoryViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<CategoryViewModel>>> HandleAsync(GetCategoriesQuery query)
    {
        var categoryCounts = await db.StorefrontCatalogueEntries.AsNoTracking()
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync();

        var subcategoryCounts = await db.StorefrontCatalogueEntries.AsNoTracking()
            .Where(e => e.Subcategory != null)
            .GroupBy(e => e.Subcategory!.Value)
            .Select(g => new { Subcategory = g.Key, Count = g.Count() })
            .ToListAsync();

        var items = Enumeration.GetAll<ProductCategory>().Select(category =>
        {
            var subcategories = Enumeration.GetAll<ProductSubcategory>().Where(s => s.Category == category)
                .Select(s => new SubcategoryViewModel(s.DisplayName, subcategoryCounts.FirstOrDefault(sc => sc.Subcategory == s.Value)?.Count ?? 0))
                .ToList();

            var count = categoryCounts.FirstOrDefault(c => c.Category == category.Value)?.Count ?? 0;
            return new CategoryViewModel(category.DisplayName, count, subcategories);
        }).ToList();

        return new QueryResponse<IReadOnlyList<CategoryViewModel>> { Result = items };
    }
}
