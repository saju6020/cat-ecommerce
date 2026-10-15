using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 12.4/12.6 -- the checklist is exactly
/// <c>ProductPublicationRules.Evaluate</c>, the same pure function the publish command runs.
///
/// The ROOT load below is the one deliberate aggregate read in the Shop read side: ADR-095 has the
/// staff read share <c>ProductPublicationRules.Evaluate(product, facts)</c> with the publish command,
/// and that pure rule takes the aggregate. Everything else here (component SKUs, prices, related
/// products, the tax class) is a column projection (.ai/read-side-pattern.md section 4).</summary>
public sealed class GetStaffProductDetailQueryHandler(
    CatEcommerceDbContext db, TimeProvider timeProvider, Microsoft.Extensions.Options.IOptions<CatEcommerce.Infrastructure.Configuration.ShopOptions> shopOptions)
    : IQueryHandlerAsync<GetStaffProductDetailQuery, StaffProductDetailViewModel?>
{
    public async Task<QueryResponse<StaffProductDetailViewModel?>> HandleAsync(GetStaffProductDetailQuery query)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == query.ProductId);
        if (product is null) return new QueryResponse<StaffProductDetailViewModel?> { Result = null };

        var now = timeProvider.GetUtcNow();
        var skuIds = product.Skus.Select(s => s.Id).ToList();
        var prices = await db.PriceRows().Where(p => skuIds.Contains(p.SkuId)).ToListAsync();

        var taxClassActive = await db.Set<TaxClassAggregate>().AsNoTracking()
            .Where(t => t.Id == product.TaxClassId).Select(t => (bool?)t.IsActive).FirstOrDefaultAsync() ?? false;

        var pricedSkuIds = skuIds.Where(id => prices.Any(p => p.SkuId == id && p.EffectiveFrom <= now)).ToList();

        // Bundle component facts and the curated links' target products: one projected read each.
        var componentSkuIds = product.BundleComponents.Select(c => c.ComponentSkuId).ToList();
        var componentRows = componentSkuIds.Count == 0
            ? []
            : await db.SkuRows().Where(r => componentSkuIds.Contains(r.SkuId)).ToListAsync();

        var componentFacts = product.BundleComponents.Select(c =>
        {
            var row = componentRows.FirstOrDefault(r => r.SkuId == c.ComponentSkuId);
            return new BundleComponentFact(
                c.ComponentSkuId, row?.SkuCode ?? c.ComponentSkuId.ToString(), row?.SkuStatus == SkuStatus.Active,
                row?.ProductStatus == ProductStatus.Active, row?.Kind == ProductKind.Bundle, row?.PrescriptionRequired ?? false);
        }).ToList();

        var facts = new PublicationFacts(pricedSkuIds, taxClassActive, componentFacts);
        var checklist = ProductPublicationRules.Evaluate(product, facts)
            .Select(i => new PublicationChecklistItemViewModel(i.Item, i.IsBlocking, i.IsMet, i.Message))
            .ToList();

        // US-SHP-02 AC1 / US-SHP-07: Inventory's figure per SKU, null when the view has no row ("never reported").
        var stockBySku = await SkuSellability.StockAsync(db, skuIds, shopOptions.Value.EffectiveStorefrontBranchId);

        var skuViewModels = product.Skus.Select(s =>
        {
            var skuPrices = prices.Where(p => p.SkuId == s.Id).ToList();
            var current = SkuPricing.PriceAt(skuPrices, p => p.EffectiveFrom, now);
            var scheduled = skuPrices.Where(p => p.EffectiveFrom > now).OrderBy(p => p.EffectiveFrom).FirstOrDefault();

            return new StaffSkuViewModel(
                s.Id, s.Code, s.Barcode, s.SizeOrWeight, s.Flavour, s.Colour, s.PackQuantity, s.Status.DisplayName,
                current is null ? null : new Money(current.Amount, current.CurrencyCode).ToViewModel(),
                scheduled is null ? null : new Money(scheduled.Amount, scheduled.CurrencyCode).ToViewModel(),
                s.Label(product.Name),
                stockBySku.TryGetValue(s.Id, out var stock) ? new StaffSkuStockViewModel(stock.SellableQuantity, stock.NextExpiresOn, now) : null,
                s.ShippingWeightGrams);
        }).ToList();

        var relatedIds = product.RelatedProducts.Select(l => l.RelatedProductId).ToList();
        var relatedSummaries = relatedIds.Count == 0
            ? []
            : await db.Products.AsNoTracking().Where(p => relatedIds.Contains(p.Id)).Select(p => new { p.Id, p.Name, p.Status }).ToListAsync();

        var relatedViewModels = product.RelatedProducts.OrderBy(l => l.Kind.Value).ThenBy(l => l.SortOrder).Select(l =>
        {
            var summary = relatedSummaries.FirstOrDefault(s => s.Id == l.RelatedProductId);
            return new StaffRelatedProductViewModel(l.RelatedProductId, l.Kind.DisplayName, l.SortOrder, summary?.Name, summary?.Status.DisplayName);
        }).ToList();

        var fd = product.FoodDetails;
        var food = fd is null ? null : new FoodDetailViewModel(
            fd.Manufacturer, fd.LifeStage?.DisplayName, fd.ProteinSource?.DisplayName, fd.IngredientList,
            fd.GuaranteedAnalysis is { } ga ? new GuaranteedAnalysisViewModel(ga.ProteinPercent, ga.FatPercent, ga.FibrePercent, ga.MoisturePercent) : null,
            fd.FeedingGuide, fd.StorageInstructions);

        var images = product.Images.OrderBy(i => i.SortOrder)
            .Select(i => new StaffProductImageViewModel(i.FileId, null, i.AltText, i.SortOrder, i.IsPrimary, i.SkuId)).ToList();

        var viewModel = new StaffProductDetailViewModel(
            product.Id, product.Version, product.Kind.DisplayName, product.Name, product.Brand, product.Description,
            product.Category.DisplayName, product.Subcategory?.DisplayName, product.Tags, product.Slug,
            product.Status.DisplayName, product.FirstPublishedOn, product.TaxClassId, product.CountryOfOriginCode,
            product.PrescriptionRequired, product.IsExpiryTracked, product.ListedOnStorefront, skuViewModels, checklist,
            product.BundleComponents.Select(c =>
            {
                var row = componentRows.FirstOrDefault(r => r.SkuId == c.ComponentSkuId);
                return new StaffBundleComponentViewModel(c.ComponentSkuId, row?.SkuCode, row?.ProductName, c.Quantity,
                    row?.SkuStatus == SkuStatus.Active && row?.ProductStatus == ProductStatus.Active);
            }).ToList(),
            relatedViewModels,
            product.Translations.OrderBy(t => t.Locale).Select(t => new StaffTranslationViewModel(t.Locale, t.Name, t.Description, t.IngredientList, t.FeedingGuide)).ToList(),
            food, images, product.HealthFocus.Select(h => h.DisplayName).ToList());

        return new QueryResponse<StaffProductDetailViewModel?> { Result = viewModel };
    }
}
