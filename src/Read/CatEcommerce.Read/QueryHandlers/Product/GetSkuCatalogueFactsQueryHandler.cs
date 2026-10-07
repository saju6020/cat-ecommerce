using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 8.12 -- catalogue facts for Inventory (D-2: a receipt for an
/// expiry-tracked SKU needs an expiry) and Reporting (revenue by country of origin, US-RPT-03).
/// Facts only, never a decision (ADR-092). One projected read.</summary>
public sealed class GetSkuCatalogueFactsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetSkuCatalogueFactsQuery, IReadOnlyList<SkuCatalogueFactViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<SkuCatalogueFactViewModel>>> HandleAsync(GetSkuCatalogueFactsQuery query)
    {
        var skuIds = query.SkuIds.Distinct().ToList();
        var rows = await db.SkuRows().Where(r => skuIds.Contains(r.SkuId)).ToListAsync();

        IReadOnlyList<SkuCatalogueFactViewModel> facts = rows.Select(r =>
            new SkuCatalogueFactViewModel(r.SkuId, r.ProductId, r.Category.DisplayName, r.IsFood, r.IsExpiryTracked, r.CountryOfOriginCode, r.SkuCode, r.Barcode)).ToList();

        return new QueryResponse<IReadOnlyList<SkuCatalogueFactViewModel>> { Result = facts };
    }
}
