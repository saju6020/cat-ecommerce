using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Persistence.Views;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>
/// HO-SHP-001 section 8.10 / 12.4 -- US-SHP-08. Curated "frequently bought together" and up-sell
/// links, plus a computed "recommended for your cat's life stage" block, every one filtered by
/// <see cref="RecommendationExclusionPolicy"/> (out of stock, the product itself, and prescription
/// products the caller cannot buy -- always for a guest, and for a customer unless
/// <c>IDietAuthorisationGate</c> finds an authorised cat for one of the product's SKUs). Column
/// projections only; availability is fail-closed unless the interim flag is on.
/// </summary>
public sealed class GetProductRecommendationsQueryHandler(
    CatEcommerceDbContext db, IDietAuthorisationGate dietGate, IBusinessSettings businessSettings, IContentLocaleSettings localeSettings,
    TimeProvider timeProvider, IOptions<ShopOptions> shopOptions)
    : IQueryHandlerAsync<GetProductRecommendationsQuery, RecommendationsViewModel?>
{
    private const int MaxLifeStageItems = 8;
    private const int LifeStageCandidatePool = 50;

    public async Task<QueryResponse<RecommendationsViewModel?>> HandleAsync(GetProductRecommendationsQuery query)
    {
        var interim = shopOptions.Value.InterimAvailabilityFromPrice;

        var product = await db.Products.AsNoTracking()
            .Where(p => p.Slug == query.Slug && p.Status == ProductStatus.Active && p.ListedOnStorefront)
            .Select(p => new
            {
                p.Id,
                IsFood = p.Category == ProductCategory.CatFood,
                OwnLifeStage = p.FoodDetails == null ? null : p.FoodDetails.LifeStage,
                Links = p.RelatedProducts.Select(l => new { l.RelatedProductId, l.Kind, l.SortOrder }).ToList()
            })
            .FirstOrDefaultAsync();

        if (product is null) return new QueryResponse<RecommendationsViewModel?> { Result = null };

        var isGuest = query.CustomerId is null;

        var linkedIds = product.Links.Select(l => l.RelatedProductId).Distinct().ToList();
        var linkedEntries = linkedIds.Count == 0
            ? []
            : await db.StorefrontCatalogueEntries.AsNoTracking().Where(e => linkedIds.Contains(e.ProductId)).ToListAsync();

        var lifeStages = await ResolveLifeStagesAsync(product.IsFood ? product.OwnLifeStage : null, query.CustomerId);
        var candidates = await LoadLifeStageCandidatesAsync(product.Id, lifeStages.Stages, interim);

        var buyableRx = await ResolveBuyablePrescriptionProductsAsync(query.CustomerId, linkedEntries.Concat(candidates).DistinctBy(e => e.ProductId));

        IReadOnlyList<CatalogueItem> CuratedBlock(RelatedProductKind kind) => product.Links
            .Where(l => l.Kind == kind).OrderBy(l => l.SortOrder)
            .Select(l => linkedEntries.FirstOrDefault(e => e.ProductId == l.RelatedProductId))
            .Where(e => e is not null && IsIncluded(e, product.Id, isGuest, buyableRx, interim))
            .Select(e => GetCatalogueQueryHandler.ToCatalogueItem(e!, interim)).ToList();

        var lifeStageItems = candidates
            .Where(e => IsIncluded(e, product.Id, isGuest, buyableRx, interim))
            .Take(MaxLifeStageItems)
            .Select(e => GetCatalogueQueryHandler.ToCatalogueItem(e, interim)).ToList();

        // US-SHP-14: names in the requested language, per-field fallback to the default.
        var result = new RecommendationsViewModel(
            await CatalogueItemLocaliser.ApplyAsync(db, localeSettings, CuratedBlock(RelatedProductKind.FrequentlyBoughtTogether), query.Locale),
            await CatalogueItemLocaliser.ApplyAsync(db, localeSettings, CuratedBlock(RelatedProductKind.UpSell), query.Locale),
            new LifeStageRecommendations(lifeStages.Basis, lifeStages.Stages.Select(s => s.DisplayName).ToList(),
                await CatalogueItemLocaliser.ApplyAsync(db, localeSettings, lifeStageItems, query.Locale)));

        return new QueryResponse<RecommendationsViewModel?> { Result = result };
    }

    private static bool IsIncluded(StorefrontCatalogueEntry entry, Guid productId, bool isGuest, HashSet<Guid> buyableRx, bool interim) =>
        RecommendationExclusionPolicy.IsIncluded(
            isActiveAndListed: true, inStock: interim && entry.InStock, isSameProduct: entry.ProductId == productId,
            prescriptionRequired: entry.PrescriptionRequired, isGuest: isGuest, callerHasEligibleCat: buyableRx.Contains(entry.ProductId));

    /// <summary>The signed-in customer's alive cats' life stages (section 8.13's derivation) when
    /// they have any, else the product's own life stage.</summary>
    private async Task<(string Basis, IReadOnlyList<LifeStage> Stages)> ResolveLifeStagesAsync(LifeStage? ownLifeStage, Guid? customerId)
    {
        if (customerId is not null)
        {
            var cats = await db.Set<CatAggregate>().AsNoTracking()
                .Where(c => c.OwnerId == customerId && c.LifeStatus == CatLifeStatus.Alive)
                .Select(c => new { c.DateOfBirth, c.DateOfBirthPrecision })
                .ToListAsync();

            var today = await ShopBusinessDate.TodayAsync(db, timeProvider);
            var kittenMax = businessSettings.GetInt(SettingKeys.ShopRecommendationKittenMaxAgeMonths);
            var seniorFrom = businessSettings.GetInt(SettingKeys.ShopRecommendationSeniorFromAgeYears);

            var stages = cats
                .Select(c => CatLifeStageDerivation.Derive(c.DateOfBirth, c.DateOfBirthPrecision, today, kittenMax, seniorFrom).Stage)
                .Where(s => s is not null).Select(s => s!).Distinct().ToList();

            if (stages.Count > 0) return ("YourCats", stages);
        }

        return ("ThisProduct", ownLifeStage is null ? [] : [ownLifeStage]);
    }

    private async Task<List<StorefrontCatalogueEntry>> LoadLifeStageCandidatesAsync(Guid productId, IReadOnlyList<LifeStage> stages, bool interim)
    {
        if (stages.Count == 0 || !interim) return [];

        var food = ProductCategory.CatFood.Value;
        var source = db.StorefrontCatalogueEntries.AsNoTracking()
            .Where(e => e.Category == food && e.InStock && e.ProductId != productId && e.LifeStage != null);

        if (!stages.Contains(LifeStage.AllLifeStages))
        {
            var stageValues = stages.Select(s => s.Value).ToList();
            var allValue = LifeStage.AllLifeStages.Value;
            source = source.Where(e => stageValues.Contains(e.LifeStage!.Value) || e.LifeStage == allValue);
        }

        return await source
            .OrderByDescending(e => e.UnitsSold90d).ThenByDescending(e => e.FirstPublishedOn).ThenBy(e => e.ProductId)
            .Take(LifeStageCandidatePool)
            .ToListAsync();
    }

    /// <summary>Prescription products among the candidates the caller can actually buy: a guest can
    /// buy none; a customer can buy one when the gate names an authorised cat for any active SKU.</summary>
    private async Task<HashSet<Guid>> ResolveBuyablePrescriptionProductsAsync(Guid? customerId, IEnumerable<StorefrontCatalogueEntry> entries)
    {
        var buyable = new HashSet<Guid>();
        if (customerId is null) return buyable;

        var prescriptionIds = entries.Where(e => e.PrescriptionRequired).Select(e => e.ProductId).ToList();
        if (prescriptionIds.Count == 0) return buyable;

        var skusByProduct = await db.SkuRows()
            .Where(r => prescriptionIds.Contains(r.ProductId) && r.SkuStatus == SkuStatus.Active)
            .Select(r => new { r.ProductId, r.SkuId })
            .ToListAsync();

        foreach (var group in skusByProduct.GroupBy(r => r.ProductId))
        {
            foreach (var row in group)
            {
                var cats = await dietGate.GetAuthorisedCatsAsync(customerId.Value, row.SkuId, CancellationToken.None);
                if (cats.Count == 0) continue;

                buyable.Add(group.Key);
                break;
            }
        }

        return buyable;
    }
}
