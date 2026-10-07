using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 8.12 / 8.5 -- the snapshot source: the SKU price at the moment of
/// the transaction (<c>SkuPricing.PriceAt</c> over projected rows) and its product tax class.
/// Orders and Billing store the result on the line and never reprice from <c>product.sku_price</c>.
/// A SKU id that does not exist is simply absent from the result. Two projected reads.</summary>
public sealed class GetSkuSaleFactsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetSkuSaleFactsQuery, IReadOnlyList<SkuSaleFactViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<SkuSaleFactViewModel>>> HandleAsync(GetSkuSaleFactsQuery query)
    {
        var skuIds = query.SkuIds.Distinct().ToList();
        var rows = await db.SkuRows().Where(r => skuIds.Contains(r.SkuId)).ToListAsync();
        var prices = await db.PriceRows().Where(p => skuIds.Contains(p.SkuId) && p.EffectiveFrom <= query.AsOf).ToListAsync();

        var rxBundles = await db.BundlesWithPrescriptionComponentAsync(rows.Where(r => r.Kind == CatEcommerce.Shared.Common.Enums.ProductKind.Bundle).Select(r => r.ProductId).Distinct().ToList());

        IReadOnlyList<SkuSaleFactViewModel> facts = rows.Select(r =>
        {
            var price = SkuPricing.PriceAt(prices.Where(p => p.SkuId == r.SkuId), p => p.EffectiveFrom, query.AsOf);
            return new SkuSaleFactViewModel(
                r.SkuId, r.ProductId, r.ProductName, r.Label, r.SkuCode, r.Kind.DisplayName, r.ProductStatus.DisplayName, r.SkuStatus.DisplayName,
                r.ListedOnStorefront, r.PrescriptionRequired || rxBundles.Contains(r.ProductId), r.TaxClassId,
                price is null ? null : new Money(price.Amount, price.CurrencyCode).ToViewModel());
        }).ToList();

        return new QueryResponse<IReadOnlyList<SkuSaleFactViewModel>> { Result = facts };
    }
}
