using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 8.10 -- the composition contract. The bundle SKU has no stock of its
/// own: Orders reserves and Inventory deducts each component from its own stock (US-SHP-12 AC1,
/// second half). Null for a non-bundle SKU. Two projected reads, no aggregate.</summary>
public sealed class GetBundleCompositionQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetBundleCompositionQuery, IReadOnlyList<BundleCompositionLineViewModel>?>
{
    public async Task<QueryResponse<IReadOnlyList<BundleCompositionLineViewModel>?>> HandleAsync(GetBundleCompositionQuery query)
    {
        var productId = await db.SkuRows()
            .Where(r => r.SkuId == query.BundleSkuId && r.Kind == ProductKind.Bundle)
            .Select(r => (Guid?)r.ProductId)
            .FirstOrDefaultAsync();

        IReadOnlyList<BundleCompositionLineViewModel>? result = null;
        if (productId is not null)
        {
            var rows = await db.BundleComponentRows().Where(c => c.ProductId == productId).OrderBy(c => c.ComponentSkuId).ToListAsync();
            result = rows.Select(c => new BundleCompositionLineViewModel(c.ComponentSkuId, c.Quantity)).ToList();
        }

        return new QueryResponse<IReadOnlyList<BundleCompositionLineViewModel>?> { Result = result };
    }
}
