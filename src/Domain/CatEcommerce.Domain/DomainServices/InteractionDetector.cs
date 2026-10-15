using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One active medicine the new one interacts with: the highest severity found, every note joined, and whether it is the same medicine again.</summary>
public sealed record DetectedInteraction(
    string OtherName, string? OtherPrescriptionNumber, ActiveMedicineSource Source, InteractionSeverity Severity, string Notes, bool IsDuplicate);

/// <summary>
/// HO-PHM-001 §8.6 (ADR-140) -- interactions are listed per medicine by people, so they are checked in BOTH directions: for the new medicine N and
/// each active medicine A, an interaction exists if N lists A (by id, or by a term matching one of A's name, ingredients or classes) or A lists N the
/// same way. Duplicates collapse to the highest severity with every note joined. The same medicine already active is a <c>Major</c> interaction of its
/// own. Pure.
/// </summary>
public static class InteractionDetector
{
    public static IReadOnlyList<DetectedInteraction> Detect(FormularyMedicineSnapshot medicine, IEnumerable<ActiveMedicineFact> activeMedicines, string catName)
    {
        var newTerms = AllergyConflictDetector.MatchTerms(medicine);
        var results = new List<DetectedInteraction>();

        foreach (var active in activeMedicines)
        {
            var found = new List<(InteractionSeverity Severity, string Note)>();
            var activeTerms = ActiveTerms(active);
            var isDuplicate = active.MedicineId is { } id && id == medicine.MedicineId;

            if (isDuplicate)
            {
                var sentence = active.Source == ActiveMedicineSource.Prescription && active.PrescriptionNumber is { } number
                    ? PharmacyMessages.DuplicateActivePrescription(catName, active.Name, number)
                    : $"{catName} is already taking {active.Name}.";
                found.Add((InteractionSeverity.Major, sentence));
            }

            foreach (var listed in medicine.Interactions)
            {
                if (Targets(listed, active.MedicineId, activeTerms)) found.Add((listed.Severity, listed.Note));
            }

            foreach (var listed in active.Interactions)
            {
                if (Targets(listed, medicine.MedicineId, newTerms)) found.Add((listed.Severity, listed.Note));
            }

            if (found.Count == 0) continue;

            var highest = found.Max(f => f.Severity.Value);
            var severity = Enumeration.FromValue<InteractionSeverity>(highest);
            var notes = string.Join(" ", found.Select(f => f.Note).Distinct(StringComparer.Ordinal));
            results.Add(new DetectedInteraction(active.Name, active.PrescriptionNumber, active.Source, severity, notes, isDuplicate));
        }

        return results.OrderByDescending(r => r.Severity.Value).ThenBy(r => r.OtherName, StringComparer.Ordinal).ToList();
    }

    private static IReadOnlyList<string> ActiveTerms(ActiveMedicineFact active) =>
        new[] { MedicineTermNormaliser.Normalise(active.Name) }
            .Concat(active.Ingredients.Select(MedicineTermNormaliser.Normalise))
            .Concat(active.Classes.Select(MedicineTermNormaliser.Normalise))
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static bool Targets(InteractionSnapshot listed, Guid? otherMedicineId, IReadOnlyList<string> otherTerms)
    {
        if (listed.InteractsWithMedicineId is { } id) return otherMedicineId == id;
        if (listed.InteractsWithTerm is not { Length: > 0 } term) return false;

        var normalised = MedicineTermNormaliser.Normalise(term);
        return normalised.Length > 0 && otherTerms.Any(t => t == normalised || MedicineTermNormaliser.ContainsWholeWords(t, normalised));
    }
}
