namespace CatEcommerce.Shared.Common.Constants;

/// <summary>One declared section of a cat's clinical history (HO-EMR-001 §8.7).</summary>
public sealed record ClinicalHistorySectionDefinition(string Key, string Owner);

/// <summary>
/// HO-EMR-001 §8.7 / ADR-128 -- every section and the area that owns it. A section whose provider is
/// not registered reports <c>available: false</c> with its owner, so a later area adds its panel by
/// registering one <c>IClinicalHistorySectionProvider</c> without editing this slice's read.
/// </summary>
public static class ClinicalHistorySections
{
    public const string Timeline = "timeline";
    public const string Visits = "visits";
    public const string Diagnoses = "diagnoses";
    public const string LabResults = "lab-results";
    public const string Photos = "photos";
    public const string Vaccinations = "vaccinations";
    public const string ParasiteTreatments = "parasite-treatments";
    public const string Weights = "weights";
    public const string Prescriptions = "prescriptions";
    public const string BoardingStays = "boarding-stays";
    public const string FoodPurchases = "food-purchases";

    public static readonly IReadOnlyList<ClinicalHistorySectionDefinition> All =
    [
        new(Timeline, "HO-EMR-001"),
        new(Visits, "HO-EMR-001"),
        new(Diagnoses, "HO-EMR-001"),
        new(LabResults, "HO-EMR-001"),
        new(Photos, "HO-EMR-001"),
        new(Vaccinations, "HO-CAT-001"),
        new(ParasiteTreatments, "HO-CAT-001"),
        new(Weights, "HO-CAT-001"),
        new(Prescriptions, "HO-PHM-001"),
        new(BoardingStays, "HO-BRD-001"),
        new(FoodPurchases, "HO-ORD-001"),
    ];

    public static ClinicalHistorySectionDefinition? Find(string key) =>
        All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));
}
