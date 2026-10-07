using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-SHP-001 §15 Phase H -- U-H1 (life stage), U-H2 (allergen matching), U-H3 (reasons),
/// U-H4 (prescription for another cat), U-H5 (ranking), U-H6 (content resolution).</summary>
public sealed class FoodRecommendationAndLocalisationTests
{
    private static readonly DateOnly Today = new(2026, 10, 4);
    private static readonly RecommendationThresholds Thresholds = new(KittenMaxAgeMonths: 12, SeniorFromAgeYears: 7, WeightManagementFromKg: 6.00m);

    private static CatRecommendationFacts Cat(
        DateOnly? dateOfBirth, NeuterStatus? neuter = null, decimal? weight = null, string[]? allergens = null, string[]? conditions = null,
        DateOfBirthPrecision? precision = null) =>
        new(dateOfBirth, precision ?? DateOfBirthPrecision.Exact, neuter ?? NeuterStatus.Unknown, weight, allergens ?? [], conditions ?? []);

    private static RecommendationCandidate Food(
        string name, LifeStage? lifeStage = null, HealthFocus[]? focus = null, string? ingredients = null,
        bool prescription = false, bool inStock = true, bool authorised = false, int unitsSold = 0, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), name, lifeStage ?? LifeStage.Adult, focus ?? [], ingredients, prescription, inStock, authorised, unitsSold);

    // ── U-H1 ────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(-8, 0, "Kitten")]   // 8 months
    [InlineData(0, -3, "Adult")]    // 3 years
    [InlineData(0, -7, "Senior")]   // 7 years -- the threshold itself is Senior
    public void U_H1_LifeStageFromDateOfBirth(int months, int years, string expected)
    {
        var dateOfBirth = Today.AddYears(years).AddMonths(months);

        var derived = CatLifeStageDerivation.Derive(dateOfBirth, DateOfBirthPrecision.Exact, Today, Thresholds.KittenMaxAgeMonths, Thresholds.SeniorFromAgeYears);

        Assert.Equal(expected, derived.Stage!.DisplayName);
        Assert.False(derived.IsEstimated);
    }

    [Fact]
    public void U_H1_NoDateOfBirth_NoLifeStage_AndTheResultSaysSo()
    {
        var derived = CatLifeStageDerivation.Derive(null, DateOfBirthPrecision.Exact, Today, 12, 7);
        Assert.Null(derived.Stage);

        var result = FoodRecommendationPolicy.Recommend(Cat(null, NeuterStatus.Neutered), [Food("Sterile", focus: [HealthFocus.Sterilised])], Thresholds, Today);
        Assert.Contains("AddDateOfBirth", result.Hints);
        Assert.Null(result.LifeStage.Stage);
    }

    [Fact]
    public void U_H1_EstimatedTwoYears_IsAdult_AndFlaggedAsEstimated()
    {
        var derived = CatLifeStageDerivation.Derive(Today.AddYears(-2), DateOfBirthPrecision.Estimated, Today, 12, 7);

        Assert.Equal(LifeStage.Adult, derived.Stage);
        Assert.True(derived.IsEstimated);

        var result = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-2), precision: DateOfBirthPrecision.Estimated), [Food("Adult Food")], Thresholds, Today);
        var reason = Assert.Single(Assert.Single(result.Items).Reasons);
        Assert.Equal(RecommendationReason.LifeStageMatch, reason.Code);
        Assert.Equal("true", reason.Values["about"]);
    }

    [Fact]
    public void U_H1_AKittenIsOfferedKittenFoodAndAllLifeStagesFood_NotAdultFood()
    {
        var kitten = Food("Kitten Food", LifeStage.Kitten);
        var all = Food("All Stages", LifeStage.AllLifeStages);
        var adult = Food("Adult Food", LifeStage.Adult);

        var result = FoodRecommendationPolicy.Recommend(Cat(Today.AddMonths(-4)), [kitten, all, adult], Thresholds, Today);

        Assert.Equal([kitten.ProductId, all.ProductId], result.Items.Select(i => i.ProductId).OrderBy(i => i == kitten.ProductId ? 0 : 1));
        Assert.DoesNotContain(result.Items, i => i.ProductId == adult.ProductId);
    }

    // ── U-H2 ────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Chicken", "Chicken meal, rice", true)]
    [InlineData("Chicken", "chickens", true)]
    [InlineData("Chicken", "chickpea flour", false)]
    [InlineData("fish", "Fish oil", true)]
    [InlineData("Fish oil", "salmon, fish oil, rice", true)]
    [InlineData("Fish oil", "fish meal", false)]
    [InlineData("egg", "dried eggs and yeast", true)]
    [InlineData("chicken", "", false)]
    [InlineData("", "chicken", false)]
    [InlineData("Eggs", "egg", true)]
    [InlineData("Peas", "pea protein", true)]
    [InlineData("Oats", "oat flour", true)]
    [InlineData("egg", "dried eggs", true)]
    [InlineData("pea", "green peas", true)]
    [InlineData("oat", "oats", true)]
    [InlineData("Fish oils", "fish oil", true)]
    [InlineData("pea", "peach", false)]
    [InlineData("oat", "boat", false)]
    // Row 39: y/ies, ves and es plurals, both directions.
    [InlineData("anchovy", "dried anchovies", true)]
    [InlineData("anchovies", "anchovy paste", true)]
    [InlineData("berry", "blueberries", false)]
    [InlineData("berry", "wild berries", true)]
    [InlineData("berries", "cranberry extract", false)]
    [InlineData("cranberries", "cranberry extract", true)]
    [InlineData("leaf", "spinach leaves", true)]
    [InlineData("potato", "potatoes, rice", true)]
    [InlineData("anchovy", "anchor", false)]
    public void U_H2_AllergenMatching_IsByWholeToken(string allergen, string ingredients, bool expected) =>
        Assert.Equal(expected, AllergenMatcher.Matches(allergen, ingredients));

    [Fact]
    public void U_H2_AProductWhoseIngredientsMatchARecordedAllergy_IsExcluded_AndCounted()
    {
        var safe = Food("Safe", ingredients: "salmon, rice");
        var chicken = Food("Poultry", ingredients: "chicken meal, rice");
        var chickpea = Food("Pulses", ingredients: "chickpea flour");

        var result = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3), allergens: ["Chicken"]), [safe, chicken, chickpea], Thresholds, Today);

        Assert.DoesNotContain(result.Items, i => i.ProductId == chicken.ProductId);
        Assert.Contains(result.Items, i => i.ProductId == safe.ProductId);
        Assert.Contains(result.Items, i => i.ProductId == chickpea.ProductId);
        var exclusion = Assert.Single(result.ExcludedForAllergy);
        Assert.Equal("Chicken", exclusion.Allergen);
        Assert.Equal(1, exclusion.Count);
    }

    [Fact]
    public void U_H2_DuplicateAndCaseVariantAllergens_DoNotCrash_AndCountOnce()
    {
        var poultry = Food("Poultry Dinner", ingredients: "chicken meal, rice");
        var safe = Food("Salmon Dinner", ingredients: "salmon, rice");

        var result = FoodRecommendationPolicy.Recommend(
            Cat(Today.AddYears(-3), allergens: ["Chicken", "chicken", " CHICKEN ", "Chicken", "  "]), [poultry, safe], Thresholds, Today);

        Assert.DoesNotContain(result.Items, i => i.ProductId == poultry.ProductId);
        Assert.Contains(result.Items, i => i.ProductId == safe.ProductId);
        var exclusion = Assert.Single(result.ExcludedForAllergy);
        Assert.Equal("Chicken", exclusion.Allergen);
        Assert.Equal(1, exclusion.Count);
    }

    [Fact]
    public void U_H2_UnknownIngredients_AreExcluded_ForACatWithAnAllergen_AndHinted()
    {
        var unknown = Food("No Label Yet", ingredients: null);
        var blank = Food("Blank Label", ingredients: "   ");
        var kit = Food("Mixed Kit", ingredients: "salmon", id: Guid.NewGuid()) with { IngredientsUnknown = true };
        var known = Food("Known Label", ingredients: "salmon, rice");

        var allergic = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3), allergens: ["Chicken"]), [unknown, blank, kit, known], Thresholds, Today);

        Assert.Equal([known.ProductId], allergic.Items.Select(i => i.ProductId));
        Assert.Contains("IngredientsUnknownExcluded", allergic.Hints);

        // A cat with no recorded allergen is unaffected: unknown lists are recommended as before.
        var healthy = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3)), [unknown, blank, kit, known], Thresholds, Today);
        Assert.Equal(4, healthy.Items.Count);
        Assert.DoesNotContain("IngredientsUnknownExcluded", healthy.Hints);
    }

    [Fact]
    public void U_H2_ABundleCandidate_IsCheckedAgainstTheUnionOfItsComponentsIngredients()
    {
        var kit = Food("Chicken And Rice Kit", ingredients: "chicken meal, rice; salmon, rice");

        var result = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3), allergens: ["Chicken"]), [kit], Thresholds, Today);

        Assert.Empty(result.Items);
        Assert.Equal(1, Assert.Single(result.ExcludedForAllergy).Count);
    }

    // ── U-H3 ────────────────────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<RecommendationReason> ReasonCodes(FoodRecommendationResult result, Guid productId) =>
        result.Items.Single(i => i.ProductId == productId).Reasons.Select(r => r.Code).ToList();

    [Fact]
    public void U_H3_NeuteredCat_AndASterilisedProduct_GetsTheSterilisedReason()
    {
        var sterile = Food("Sterile", focus: [HealthFocus.Sterilised]);
        var result = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3), NeuterStatus.Neutered), [sterile], Thresholds, Today);

        Assert.Contains(RecommendationReason.Sterilised, ReasonCodes(result, sterile.ProductId));
    }

    [Fact]
    public void U_H3_AnIntactCat_DoesNotGetTheSterilisedReason()
    {
        var sterile = Food("Sterile", focus: [HealthFocus.Sterilised]);
        var result = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3), NeuterStatus.Intact), [sterile], Thresholds, Today);

        Assert.DoesNotContain(RecommendationReason.Sterilised, ReasonCodes(result, sterile.ProductId));
    }

    [Fact]
    public void U_H3_HeavyAdult_GetsWeightManagement_ButAHeavyKittenDoesNot()
    {
        var light = Food("Light", LifeStage.AllLifeStages, [HealthFocus.WeightManagement]);

        var adult = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3), weight: 6.5m), [light], Thresholds, Today);
        Assert.Contains(RecommendationReason.WeightManagement, ReasonCodes(adult, light.ProductId));

        var kitten = FoodRecommendationPolicy.Recommend(Cat(Today.AddMonths(-6), weight: 6.5m), [light], Thresholds, Today);
        Assert.DoesNotContain(RecommendationReason.WeightManagement, ReasonCodes(kitten, light.ProductId));

        var slim = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3), weight: 4.2m), [light], Thresholds, Today);
        Assert.DoesNotContain(RecommendationReason.WeightManagement, ReasonCodes(slim, light.ProductId));
    }

    [Fact]
    public void U_H3_ConditionKeyword_MapsToTheProductsHealthFocus()
    {
        var urinary = Food("Urinary Care", focus: [HealthFocus.Urinary]);
        var renal = Food("Renal Care", focus: [HealthFocus.Renal]);

        var result = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3), conditions: ["FLUTD (recurrent)"]), [urinary, renal], Thresholds, Today);

        Assert.Contains(RecommendationReason.ConditionMatch, ReasonCodes(result, urinary.ProductId));
        Assert.DoesNotContain(RecommendationReason.ConditionMatch, ReasonCodes(result, renal.ProductId));
    }

    [Theory]
    [InlineData("urinary tract infection", "Urinary")]
    [InlineData("FLUTD", "Urinary")]
    [InlineData("cystitis", "Urinary")]
    [InlineData("Chronic kidney disease", "Renal")]
    [InlineData("CKD stage 2", "Renal")]
    [InlineData("gastroenteritis", "Digestive")]
    [InlineData("Obesity", "WeightManagement")]
    [InlineData("gingivitis", "Dental")]
    public void U_H3_HealthFocusKeywords_MapConditions(string condition, string expectedFocusName)
    {
        var expected = Enumeration.FromDisplayName<HealthFocus>(expectedFocusName);
        Assert.Contains(expected, HealthFocusKeywords.FocusesFor(condition));
    }

    [Fact]
    public void U_H3_HealthFocusKeywords_AdrenalIsNotRenal()
    {
        Assert.DoesNotContain(HealthFocus.Renal, HealthFocusKeywords.FocusesFor("adrenal tumour"));
        Assert.Empty(HealthFocusKeywords.FocusesFor(null));
    }

    // ── U-H4 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_H4_PrescriptionProduct_AuthorisedForAnotherCat_IsExcludedForThisOne()
    {
        var rxForOtherCat = Food("Renal Rx", prescription: true, authorised: false);
        var rxForThisCat = Food("Urinary Rx", prescription: true, authorised: true);

        var result = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3)), [rxForOtherCat, rxForThisCat], Thresholds, Today);

        Assert.DoesNotContain(result.Items, i => i.ProductId == rxForOtherCat.ProductId);
        Assert.Contains(result.Items, i => i.ProductId == rxForThisCat.ProductId);
    }

    [Fact]
    public void U_H4_OutOfStockProducts_AreNeverRecommended()
    {
        var gone = Food("Gone", inStock: false);
        Assert.Empty(FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3)), [gone], Thresholds, Today).Items);
    }

    // ── U-H5 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_H5_EqualReasons_RankByUnitsSold_ThenName_Deterministically()
    {
        var a = Food("Alpha", unitsSold: 5, id: new Guid("00000000-0000-0000-0000-000000000001"));
        var b = Food("Bravo", unitsSold: 5, id: new Guid("00000000-0000-0000-0000-000000000002"));
        var c = Food("Charlie", unitsSold: 50, id: new Guid("00000000-0000-0000-0000-000000000003"));

        var first = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3)), [b, a, c], Thresholds, Today).Items.Select(i => i.ProductId).ToList();
        var second = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3)), [c, a, b], Thresholds, Today).Items.Select(i => i.ProductId).ToList();

        Assert.Equal([c.ProductId, a.ProductId, b.ProductId], first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void U_H5_MoreReasonsRankFirst_AndAtMostTwelveAreReturned()
    {
        var sterile = Food("Zulu Sterile", focus: [HealthFocus.Sterilised], unitsSold: 0);
        var plain = Enumerable.Range(0, 20).Select(i => Food($"Plain {i:00}", unitsSold: 1000 + i)).ToList();

        var result = FoodRecommendationPolicy.Recommend(Cat(Today.AddYears(-3), NeuterStatus.Neutered), [.. plain, sterile], Thresholds, Today);

        Assert.Equal(FoodRecommendationPolicy.MaxResults, result.Items.Count);
        Assert.Equal(sterile.ProductId, result.Items[0].ProductId);
    }

    // ── U-H6 ────────────────────────────────────────────────────────────────────────────────────

    private static readonly string[] Enabled = ["en", "bn"];

    [Fact]
    public void U_H6_PerFieldFallback_NameInBn_DescriptionFallsBackToEn()
    {
        var translations = new List<ProductTranslation> { new("bn", "বিড়ালের খাবার", "", "মুরগি", null) };

        var resolved = ProductContentLocalisation.Resolve("Cat Food", "A description", "chicken", "Feed twice a day", translations, "bn", "en", Enabled);

        Assert.Equal("বিড়ালের খাবার", resolved.Name);
        Assert.Equal("A description", resolved.Description);
        Assert.Equal("মুরগি", resolved.IngredientList);
        Assert.Equal("Feed twice a day", resolved.FeedingGuide);
        Assert.Equal("bn", resolved.Locale);
        Assert.Equal("bn", resolved.FieldLocales["name"]);
        Assert.Equal("en", resolved.FieldLocales["description"]);
        Assert.Equal("bn", resolved.FieldLocales["ingredientList"]);
        Assert.Equal("en", resolved.FieldLocales["feedingGuide"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("xx-YY")]
    public void U_H6_DefaultOrUnknownOrDisabledLocale_ResolvesToTheDefault_NeverAnError(string? requested)
    {
        var translations = new List<ProductTranslation> { new("bn", "বিড়াল", "বর্ণনা", "মুরগি", "দিনে দুইবার") };

        var resolved = ProductContentLocalisation.Resolve("Cat Food", "A description", "chicken", "Feed", translations, requested, "en", Enabled);

        Assert.Equal("Cat Food", resolved.Name);
        Assert.Equal("en", resolved.Locale);
        Assert.All(resolved.FieldLocales.Values, v => Assert.Equal("en", v));
    }

    [Fact]
    public void U_H6_ALocaleNoLongerEnabled_IsIgnored_EvenWithAStoredTranslation()
    {
        var translations = new List<ProductTranslation> { new("bn", "বিড়াল", null, null, null) };

        var resolved = ProductContentLocalisation.Resolve("Cat Food", "d", null, null, translations, "bn", "en", ["en"]);

        Assert.Equal("Cat Food", resolved.Name);
    }
}
