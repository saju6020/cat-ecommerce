using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

public sealed record AllergyConflict(string Allergen, string Reaction, AllergySeverity Severity, string MatchedTerm);

/// <summary>
/// HO-PHM-001 §8.6 (ADR-140) -- allergy matching by normalised term over the medicine's name, active ingredients and drug classes. HO-CAT-001 stores
/// allergens as free text, so this warns on likely matches and cannot promise to catch a misspelling (a documented limitation, stated on the screen).
/// An allergy conflicts when, after normalisation, the allergen equals a match term, contains one as whole words, or is contained in one as whole
/// words and is at least four letters long. Pure.
/// </summary>
public static class AllergyConflictDetector
{
    public const int MinimumContainedAllergenLength = 4;

    public static IReadOnlyList<AllergyConflict> Detect(FormularyMedicineSnapshot medicine, IEnumerable<AllergyFact> allergies)
    {
        var terms = MatchTerms(medicine);
        var conflicts = new List<AllergyConflict>();

        foreach (var allergy in allergies)
        {
            var allergen = MedicineTermNormaliser.Normalise(allergy.Allergen);
            if (allergen.Length == 0) continue;

            var matched = terms.FirstOrDefault(term => Matches(allergen, term));
            if (matched is not null) conflicts.Add(new AllergyConflict(allergy.Allergen, allergy.Reaction, allergy.Severity, matched));
        }

        return conflicts;
    }

    public static IReadOnlyList<string> MatchTerms(FormularyMedicineSnapshot medicine) =>
        new[] { MedicineTermNormaliser.Normalise(medicine.Name) }
            .Concat(medicine.ActiveIngredients.Select(MedicineTermNormaliser.Normalise))
            .Concat(medicine.DrugClasses.Select(MedicineTermNormaliser.Normalise))
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static bool Matches(string allergen, string term) =>
        allergen == term
        || MedicineTermNormaliser.ContainsWholeWords(allergen, term)
        || (allergen.Length >= MinimumContainedAllergenLength && MedicineTermNormaliser.ContainsWholeWords(term, allergen));
}
