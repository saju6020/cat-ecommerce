using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Pharmacy;

/// <summary>
/// HO-PHM-001 §8.13 / §12.2 -- the ONE place the safety assessor's facts are loaded, used by the check query, issue and renew so the screen and the
/// command can never see different facts. Read-only and untracked. For one cat: the newest weight (the newest <c>cat_weight_record</c> row, ADR-023); the
/// recorded allergies; the cat's active prescriptions (active per <see cref="PrescriptionActivityRules"/>, the ONE definition) joined to their
/// CURRENT formulary data; and the profile medications -- those a formulary medicine matches by name or active ingredient become interaction candidates,
/// the rest are returned as unresolved so they are listed, never silently ignored (ADR-140).
/// </summary>
public sealed class SafetyInputLoader(CatEcommerceDbContext db, IBusinessSettings settings) : ISafetyInputLoader
{
    public async Task<SafetyInput> LoadAsync(
        Guid catId, string catName, FormularyMedicineSnapshot medicine, Dose dose, AdministrationRoute route, DoseFrequency frequency, DateOnly today, CancellationToken ct,
        Guid? excludePrescriptionId = null)
    {
        var weightRow = await db.CatWeightRecords.AsNoTracking()
            .Where(w => w.CatId == catId)
            .OrderByDescending(w => w.RecordedOn).ThenByDescending(w => w.CreatedDate)
            .Select(w => new { w.Weight.Kilograms, w.RecordedOn })
            .FirstOrDefaultAsync(ct);

        var allergyRows = await db.Cats.AsNoTracking().Where(c => c.Id == catId)
            .SelectMany(c => c.Allergies.Select(a => new { a.Allergen, a.Reaction, a.Severity }))
            .ToListAsync(ct);

        var profileMedicationRows = await db.Cats.AsNoTracking().Where(c => c.Id == catId)
            .SelectMany(c => c.Medications.Where(m => m.EndsOn == null || m.EndsOn >= today).Select(m => m.MedicationName))
            .ToListAsync(ct);

        var prescriptionRows = await db.Prescriptions.AsNoTracking()
            .Where(p => p.CatId == catId && p.Status != PrescriptionStatus.Cancelled && p.Id != excludePrescriptionId)
            .Select(p => new
            {
                p.PrescriptionNumber, p.MedicineId, p.MedicineSnapshot.Name, p.Status, p.StartsOn, p.LastDispensedOn, p.DurationDays, p.RepeatsAuthorised,
                p.SuppliesDispensed, p.ValidUntil
            })
            .ToListAsync(ct);

        var active = prescriptionRows
            .Where(p => PrescriptionActivityRules.IsActive(p.Status, p.StartsOn, p.LastDispensedOn, p.DurationDays, p.RepeatsAuthorised, p.SuppliesDispensed, p.ValidUntil, today))
            .ToList();

        // Formulary data for the medicines involved -- the active prescriptions' and anything a profile medication might name.
        var activeMedicineIds = active.Select(p => p.MedicineId).Distinct().ToList();
        IQueryable<FormularyMedicineAggregate> medicines = db.FormularyMedicines;
        if (profileMedicationRows.Count == 0) medicines = medicines.Where(m => activeMedicineIds.Contains(m.Id));

        var formulary = profileMedicationRows.Count == 0 && activeMedicineIds.Count == 0
            ? new List<FormularyMedicineSnapshot>()
            : await medicines.ToSnapshotsAsync(ct);
        var byId = formulary.ToDictionary(m => m.MedicineId);

        var activeFacts = new List<ActiveMedicineFact>();
        foreach (var prescription in active)
        {
            byId.TryGetValue(prescription.MedicineId, out var known);
            activeFacts.Add(new ActiveMedicineFact(
                prescription.MedicineId, prescription.Name, known?.ActiveIngredients ?? [], known?.DrugClasses ?? [], known?.Interactions ?? [],
                ActiveMedicineSource.Prescription, prescription.PrescriptionNumber));
        }

        var unresolved = new List<string>();
        foreach (var name in profileMedicationRows.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var normalised = MedicineTermNormaliser.Normalise(name);
            var match = formulary.FirstOrDefault(m =>
                MedicineTermNormaliser.Normalise(m.Name) == normalised || m.ActiveIngredients.Any(i => MedicineTermNormaliser.Normalise(i) == normalised));

            if (match is null)
            {
                unresolved.Add(name);
                continue;
            }

            activeFacts.Add(new ActiveMedicineFact(
                match.MedicineId, match.Name, match.ActiveIngredients, match.DrugClasses, match.Interactions, ActiveMedicineSource.ProfileMedication, null));
        }

        return new SafetyInput(
            medicine, dose, route, frequency, catName,
            weightRow is null ? null : new LatestWeight(weightRow.Kilograms, weightRow.RecordedOn), today,
            settings.GetInt(SettingKeys.PharmacyDoseWeightMaxAgeDays),
            allergyRows.Select(a => new AllergyFact(a.Allergen, a.Reaction, a.Severity)).ToList(), allergyRows.Count > 0,
            activeFacts, unresolved);
    }
}

/// <summary>HO-PHM-001 §8.3 -- <c>RX-{yyyy}-{000001}</c> from <c>pharmacy.prescription_number_seq</c>. A sequence is the one safe way to hand out gapless-enough, unique numbers
/// to concurrent prescribers; the unique index is the backstop.</summary>
public sealed class PrescriptionNumberAllocator(CatEcommerceDbContext db) : IPrescriptionNumberAllocator
{
    public async Task<string> AllocateAsync(DateTimeOffset issuedOn, CancellationToken ct)
    {
        var next = await db.Database.SqlQueryRaw<long>("SELECT nextval('pharmacy.prescription_number_seq') AS \"Value\"").SingleAsync(ct);
        return PrescriptionRules.FormatNumber(issuedOn, next);
    }
}
