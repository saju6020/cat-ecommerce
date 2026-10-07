using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Read.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Cat;

/// <summary>
/// Q5 -- feeds the medical history PDF (US-CAT-10). Diagnoses, prescriptions and lab results
/// are omitted -- those aggregates do not exist yet (section 20.5); the renderer prints an
/// explicit note naming the gap. Two-phase projection (see GetCatProfileQueryHandler) so the
/// photo collection, which Q5 never uses, is never loaded (ARCHITECTURE gate, HIGH, 2026-09-26).
/// </summary>
public sealed class GetCatMedicalHistoryQueryHandler(
    ILogger<GetCatMedicalHistoryQueryHandler> logger,
    CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetCatMedicalHistoryQuery, CatMedicalHistoryViewModel?>
{
    public async Task<QueryResponse<CatMedicalHistoryViewModel?>> HandleAsync(
        GetCatMedicalHistoryQuery query)
    {
        logger.LogInformation("Reading medical history for cat {CatId}", query.CatId);

        var projected = await db.Cats.AsNoTracking()
            .Where(c => c.Id == query.CatId)
            .Select(c => new
            {
                c.Id, c.Name, c.OwnerId, c.Species, c.Breed, c.Sex, c.DateOfBirth,
                c.DateOfBirthPrecision, c.NeuterStatus, c.NeuteredOn, c.IsHighRisk, c.HandlingNote,
                Allergies = c.Allergies
                    .Select(a => new { a.Id, a.Allergen, a.Reaction, a.Severity, a.RecordedOn })
                    .ToList(),
                ChronicConditions = c.ChronicConditions
                    .Select(cc => new { cc.Id, cc.Condition, cc.DiagnosedOn, cc.Notes })
                    .ToList(),
                Medications = c.Medications
                    .Select(m => new { m.Id, m.MedicationName, m.Dosage, m.Frequency, m.StartedOn, m.EndsOn })
                    .ToList(),
                Vaccinations = c.Vaccinations
                    .Select(v => new { v.VaccineName, v.BatchReference, v.AdministeredOn, v.NextDueOn })
                    .ToList(),
                ParasiteTreatments = c.ParasiteTreatments
                    .Select(p => new { p.TreatmentType, p.ProductName, p.AdministeredOn, p.NextDueOn })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (projected is null)
            return new QueryResponse<CatMedicalHistoryViewModel?> { Result = null };

        var weightHistory = await db.CatWeightRecords.AsNoTracking()
            .Where(w => w.CatId == query.CatId)
            .OrderBy(w => w.RecordedOn)
            .Select(w => new CatWeightPointViewModel(w.RecordedOn, w.Weight.Kilograms))
            .ToListAsync();

        var viewModel = new CatMedicalHistoryViewModel(
            projected.Id, projected.Name, projected.OwnerId, projected.Species, projected.Breed,
            projected.Sex.DisplayName, projected.DateOfBirth, projected.DateOfBirthPrecision.DisplayName,
            projected.NeuterStatus.DisplayName, projected.NeuteredOn, projected.IsHighRisk, projected.HandlingNote,
            Allergies: projected.Allergies.Select(a => new CatAllergyViewModel(
                a.Id, a.Allergen, a.Reaction, a.Severity.DisplayName, a.RecordedOn)).ToList(),
            ChronicConditions: projected.ChronicConditions.Select(c => new CatChronicConditionViewModel(
                c.Id, c.Condition, c.DiagnosedOn, c.Notes)).ToList(),
            Medications: projected.Medications.Select(m => new CatMedicationViewModel(
                m.Id, m.MedicationName, m.Dosage, m.Frequency, m.StartedOn, m.EndsOn)).ToList(),
            Vaccinations: projected.Vaccinations.Select(v => new CatMedicalHistoryVaccinationViewModel(
                v.VaccineName, v.BatchReference, v.AdministeredOn, v.NextDueOn)).ToList(),
            ParasiteTreatments: projected.ParasiteTreatments.Select(p => new CatMedicalHistoryParasiteTreatmentViewModel(
                p.TreatmentType.DisplayName, p.ProductName, p.AdministeredOn, p.NextDueOn)).ToList(),
            WeightHistory: weightHistory);

        return new QueryResponse<CatMedicalHistoryViewModel?> { Result = viewModel };
    }
}
