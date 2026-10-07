using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>
/// HO-SHP-001 section 8.13 / ADR-089 -- US-SHP-11. Loads the cat's facts READ-ONLY (allergies,
/// conditions, date of birth and precision, neuter status, owner, liveness; the latest weight) and
/// the candidates (Active, listed, in-stock food products with their life stage, health focus,
/// ingredient list, prescription flag and the gate's answer for THIS cat), then calls the pure
/// <see cref="FoodRecommendationPolicy"/>. The candidate pool is bounded (the policy needs the
/// per-candidate reasons to rank, so it cannot be a SQL TOP-N), pre-filtered in SQL by life stage
/// when the cat's is known. The policy never recommends a product the cat cannot be sold.
/// </summary>
public sealed class GetFoodRecommendationsQueryHandler(
    CatEcommerceDbContext db, IDietAuthorisationGate dietGate, IBusinessSettings businessSettings,
    IContentLocaleSettings localeSettings, TimeProvider timeProvider, Microsoft.Extensions.Options.IOptions<CatEcommerce.Infrastructure.Configuration.ShopOptions> shopOptions)
    : IQueryHandlerAsync<GetFoodRecommendationsQuery, FoodRecommendationsViewModel?>
{
    /// <summary>Row 20: the most popular in-stock foods for the cat's stage. The policy keeps twelve, so a
    /// pool of a few hundred is ample; the old 2,000 made one request cost thousands of rows and gate calls.</summary>
    private const int CandidatePool = 500;

    /// <summary>Row 20: at most this many prescription candidates are put to the diet gate per request
    /// (the rest are treated as not authorised and so never recommended), in ONE batched call.</summary>
    private const int MaxPrescriptionCandidates = 50;

    /// <summary>Row 20: an upper bound on the SKU ids sent in that one batched gate call.</summary>
    private const int MaxGateSkus = 200;

    public async Task<QueryResponse<FoodRecommendationsViewModel?>> HandleAsync(GetFoodRecommendationsQuery query)
    {
        // Ownership and liveness are resolved here, by id AND caller: another customer's cat (or a
        // deceased one) is indistinguishable from an unknown one (404).
        var cat = await db.Cats.AsNoTracking()
            .Where(c => c.Id == query.CatId && c.OwnerId == query.CustomerId && c.LifeStatus == CatLifeStatus.Alive)
            .Select(c => new
            {
                c.Id, c.Name, c.DateOfBirth, c.DateOfBirthPrecision, c.NeuterStatus,
                Allergens = c.Allergies.Select(a => a.Allergen).ToList(),
                Conditions = c.ChronicConditions.Select(x => x.Condition).ToList()
            })
            .FirstOrDefaultAsync();
        if (cat is null) return new QueryResponse<FoodRecommendationsViewModel?> { Result = null };

        var weight = await db.CatWeightRecords.AsNoTracking()
            .Where(w => w.CatId == cat.Id)
            .OrderByDescending(w => w.RecordedOn)
            .Select(w => (decimal?)w.Weight.Kilograms)
            .FirstOrDefaultAsync();

        var today = await ShopBusinessDate.TodayAsync(db, timeProvider);
        var thresholds = new RecommendationThresholds(
            businessSettings.GetInt(SettingKeys.ShopRecommendationKittenMaxAgeMonths),
            businessSettings.GetInt(SettingKeys.ShopRecommendationSeniorFromAgeYears),
            businessSettings.GetDecimal(SettingKeys.ShopRecommendationWeightManagementFromKg));

        var facts = new CatRecommendationFacts(
            cat.DateOfBirth, cat.DateOfBirthPrecision, cat.NeuterStatus, weight,
            cat.Allergens, cat.Conditions);

        var stage = CatLifeStageDerivation.Derive(cat.DateOfBirth, cat.DateOfBirthPrecision, today, thresholds.KittenMaxAgeMonths, thresholds.SeniorFromAgeYears);

        var interim = shopOptions.Value.InterimAvailabilityFromPrice;
        var entries = interim ? await LoadInStockFoodEntriesAsync(stage.Stage) : [];
        var candidates = await BuildCandidatesAsync(entries, query.CustomerId, cat.Id);

        var result = FoodRecommendationPolicy.Recommend(facts, candidates, thresholds, today);

        var entriesById = entries.ToDictionary(e => e.ProductId);
        var items = result.Items.Select(r => (r, Item: GetCatalogueQueryHandler.ToCatalogueItem(entriesById[r.ProductId], interim))).ToList();
        var localised = await CatalogueItemLocaliser.ApplyAsync(db, localeSettings, items.Select(i => i.Item).ToList(), query.Locale);

        var viewItems = items.Select((i, index) => new FoodRecommendationItemViewModel(
            localised[index],
            i.r.Reasons.Select(reason => new RecommendationReasonViewModel(reason.Code.DisplayName, reason.Values)).ToList())).ToList();

        var viewModel = new FoodRecommendationsViewModel(
            new RecommendationCatViewModel(cat.Id, cat.Name, result.LifeStage.Stage?.DisplayName, result.LifeStage.IsEstimated),
            viewItems,
            result.ExcludedForAllergy.Select(e => new AllergyExclusionViewModel(e.Allergen, e.Count)).ToList(),
            result.Hints);

        return new QueryResponse<FoodRecommendationsViewModel?> { Result = viewModel };
    }

    private async Task<List<CatEcommerce.Infrastructure.Persistence.Views.StorefrontCatalogueEntry>> LoadInStockFoodEntriesAsync(LifeStage? stage)
    {
        var food = ProductCategory.CatFood.Value;
        var source = db.StorefrontCatalogueEntries.AsNoTracking().Where(e => e.Category == food && e.InStock);

        if (stage is not null)
        {
            var stageValue = stage.Value;
            var allValue = LifeStage.AllLifeStages.Value;
            source = source.Where(e => e.LifeStage == stageValue || e.LifeStage == allValue);
        }

        return await source.OrderByDescending(e => e.UnitsSold90d).ThenByDescending(e => e.FirstPublishedOn).ThenBy(e => e.ProductId)
            .Take(CandidatePool).ToListAsync();
    }

    /// <summary>
    /// A food bundle has no ingredient list of its own: its allergen exclusion is evaluated over the UNION
    /// of its components' lists (projections, no aggregate). A food component with an unknown list makes
    /// the whole bundle unknown (fail-closed for an allergic cat); a non-food component (a toy in a kit)
    /// carries no ingredients and is ignored.
    /// </summary>
    private async Task<Dictionary<Guid, (string Combined, bool Unknown)>> LoadBundleIngredientsAsync(
        List<CatEcommerce.Infrastructure.Persistence.Views.StorefrontCatalogueEntry> entries)
    {
        var result = new Dictionary<Guid, (string, bool)>();
        var bundleIds = entries.Where(e => e.Kind == ProductKind.Bundle.Value).Select(e => e.ProductId).ToList();
        if (bundleIds.Count == 0) return result;

        var components = await db.BundleComponentRows().Where(c => bundleIds.Contains(c.ProductId)).ToListAsync();
        var componentSkuIds = components.Select(c => c.ComponentSkuId).Distinct().ToList();
        var skus = await db.SkuRows().Where(r => componentSkuIds.Contains(r.SkuId))
            .Select(r => new { r.SkuId, r.ProductId, r.Category }).ToListAsync();
        var componentProductIds = skus.Select(s => s.ProductId).Distinct().ToList();
        var lists = await db.Products.AsNoTracking().Where(p => componentProductIds.Contains(p.Id))
            .Select(p => new { p.Id, IngredientList = p.FoodDetails == null ? null : p.FoodDetails.IngredientList }).ToListAsync();

        foreach (var bundleId in bundleIds)
        {
            var unknown = false;
            var parts = new List<string>();
            foreach (var component in components.Where(c => c.ProductId == bundleId))
            {
                var sku = skus.FirstOrDefault(s => s.SkuId == component.ComponentSkuId);
                if (sku is null) { unknown = true; continue; }
                if (sku.Category != ProductCategory.CatFood) continue;

                var list = lists.FirstOrDefault(l => l.Id == sku.ProductId)?.IngredientList;
                if (string.IsNullOrWhiteSpace(list)) unknown = true;
                else parts.Add(list);
            }

            result[bundleId] = (string.Join(", ", parts), unknown);
        }

        return result;
    }

    /// <summary>Row 20: the prescription candidates (capped, most popular first -- the entries arrive in that
    /// order) whose cat-specific authorisation holds, answered by ONE batched gate call for all their active
    /// SKUs. A product is authorised when ANY of its active SKUs is.</summary>
    private async Task<HashSet<Guid>> ResolveAuthorisedPrescriptionProductsAsync(
        List<CatEcommerce.Infrastructure.Persistence.Views.StorefrontCatalogueEntry> entries,
        Dictionary<Guid, List<Guid>> skuIdsByProduct, Guid customerId, Guid catId)
    {
        var skuToProduct = new Dictionary<Guid, Guid>();
        foreach (var entry in entries.Where(e => e.PrescriptionRequired).Take(MaxPrescriptionCandidates))
        {
            if (!skuIdsByProduct.TryGetValue(entry.ProductId, out var skuIds)) continue;
            foreach (var skuId in skuIds)
            {
                if (skuToProduct.Count >= MaxGateSkus) break;
                skuToProduct[skuId] = entry.ProductId;
            }
        }

        if (skuToProduct.Count == 0) return [];

        var results = await dietGate.CheckManyAsync(customerId, catId, skuToProduct.Keys.ToList(), CancellationToken.None);
        return results.Where(r => r.Value.Outcome == DietGateOutcome.Authorised && skuToProduct.ContainsKey(r.Key))
            .Select(r => skuToProduct[r.Key]).ToHashSet();
    }

    private async Task<List<RecommendationCandidate>> BuildCandidatesAsync(
        List<CatEcommerce.Infrastructure.Persistence.Views.StorefrontCatalogueEntry> entries, Guid customerId, Guid catId)
    {
        var ids = entries.Select(e => e.ProductId).ToList();
        var details = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                IngredientList = p.FoodDetails == null ? null : p.FoodDetails.IngredientList,
                p.HealthFocus,
                SkuIds = p.Skus.Where(s => s.Status == SkuStatus.Active).Select(s => s.Id).ToList()
            })
            .ToListAsync();
        var detailsById = details.ToDictionary(d => d.Id);
        var bundleIngredients = await LoadBundleIngredientsAsync(entries);

        var authorisedProducts = await ResolveAuthorisedPrescriptionProductsAsync(entries, details.ToDictionary(d => d.Id, d => d.SkuIds), customerId, catId);

        var candidates = new List<RecommendationCandidate>();
        foreach (var entry in entries)
        {
            if (!detailsById.TryGetValue(entry.ProductId, out var detail)) continue;

            var authorised = entry.PrescriptionRequired && authorisedProducts.Contains(entry.ProductId);

            candidates.Add(new RecommendationCandidate(
                entry.ProductId, entry.Name, entry.LifeStage is null ? null : Enumeration.FromValue<LifeStage>(entry.LifeStage.Value),
                detail.HealthFocus,
                bundleIngredients.TryGetValue(entry.ProductId, out var bundle) ? bundle.Combined : detail.IngredientList,
                entry.PrescriptionRequired, entry.InStock, authorised, entry.UnitsSold90d,
                IngredientsUnknown: bundleIngredients.TryGetValue(entry.ProductId, out var kit) && kit.Unknown));
        }

        return candidates;
    }
}
