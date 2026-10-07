using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>
/// HO-SHP-001 section 8.9 / 12.4 -- US-SHP-10 AC1. Per active SKU: the prescription rule
/// (<see cref="PrescriptionPurchaseGate"/> over HO-PHM-001's IDietAuthorisationGate) and availability
/// (<see cref="ISkuAvailabilityReader"/>, fail-closed until Inventory supplies stock). The
/// prescription reason is reported first -- it is the actionable one for the customer (book a
/// consultation); out of stock is reported when the prescription rule is already satisfied.
/// Reads are column projections (SkuRow), never the aggregate.
/// </summary>
public sealed class GetPurchasabilityQueryHandler(CatEcommerceDbContext db, IDietAuthorisationGate dietGate, ISkuAvailabilityReader availabilityReader)
    : IQueryHandlerAsync<GetPurchasabilityQuery, PurchasabilityViewModel?>
{
    public async Task<QueryResponse<PurchasabilityViewModel?>> HandleAsync(GetPurchasabilityQuery query)
    {
        // Row 15: the product's existence is its own read -- a live product with no SKUs is a 200 with an
        // empty list, not a 404 (the SKU join alone cannot tell "no product" from "no SKUs").
        var productLive = await db.Products.AsNoTracking()
            .AnyAsync(p => p.Slug == query.Slug && p.Status == ProductStatus.Active && p.ListedOnStorefront);
        if (!productLive) return new QueryResponse<PurchasabilityViewModel?> { Result = null };

        var rows = await db.SkuRows()
            .Where(r => r.ProductSlug == query.Slug && r.ProductStatus == ProductStatus.Active && r.ListedOnStorefront)
            .ToListAsync();

        var activeSkus = rows.Where(r => r.SkuStatus == SkuStatus.Active).ToList();
        var rxBundles = await db.BundlesWithPrescriptionComponentAsync(activeSkus.Where(r => r.Kind == ProductKind.Bundle).Select(r => r.ProductId).Distinct().ToList());
        var availability = await availabilityReader.GetAsync(activeSkus.Select(s => s.SkuId).ToList());

        var skus = new List<SkuPurchasabilityViewModel>();
        foreach (var sku in activeSkus)
        {
            IReadOnlyList<AuthorisedCat> authorised = [];
            if (sku.PrescriptionRequired)
                authorised = await dietGate.GetAuthorisedCatsAsync(query.CustomerId, sku.SkuId, CancellationToken.None);

            // A bundle with a prescription component can carry no per-cat authorisation (OQ-14): blocked.
            var decision = rxBundles.Contains(sku.ProductId)
                ? new PrescriptionDecision(false, PurchaseBlockReason.PrescriptionAuthorisationRequired, [])
                : PrescriptionPurchaseGate.Evaluate(sku.PrescriptionRequired, query.CustomerId, authorised);

            var inStock = availability.GetValueOrDefault(sku.SkuId) == AvailabilityStatus.InStock;
            var purchasable = decision.IsPermitted && inStock;
            var reason = decision.BlockReason ?? (inStock ? null : PurchaseBlockReason.OutOfStock);

            var eligibleCats = decision.IsPermitted
                ? decision.EligibleCats.Select(c => new EligibleCatViewModel(c.CatId, c.CatName, c.ValidTo)).ToList()
                : [];

            skus.Add(new SkuPurchasabilityViewModel(sku.SkuId, purchasable, reason?.DisplayName, eligibleCats));
        }

        return new QueryResponse<PurchasabilityViewModel?> { Result = new PurchasabilityViewModel(skus) };
    }
}
