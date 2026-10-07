using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Read.QueryHandlers.StoredFile;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Cat;

/// <summary>
/// Q1 -- GET /api/cats/{catId}. Two-phase projection, the same shape as GetCatsQueryHandler:
/// the DB-translatable phase Selects plain scalars, the owned collections (nested Select) and
/// the Enumeration properties themselves (direct member access, never chained) -- this is what
/// keeps EF from materialising the whole aggregate (with every owned collection, including
/// Vaccinations and ParasiteTreatments, which Q1 never shows) through CatAggregate's private
/// constructor. DisplayName is resolved client-side afterwards (ARCHITECTURE gate, HIGH,
/// 2026-09-26 -- .ai/read-side-pattern.md: "project, never rehydrate").
///
/// Gate-fix 2026-09-27 (ARCHITECTURE HIGH): photos are served through FIL's signed URL by file id
/// (§8.6/§12.4), never a raw storage key.
///
/// Gate-fix 2026-09-27, second pass (ARCHITECTURE LOW): the metadata-plus-sign mapping is now
/// <see cref="IStoredFileSigningBatcher"/> (FIL's own, "owned once" helper), not a query against
/// <c>files.stored_file</c> re-implemented here -- still one query for the whole photo list, not
/// one dispatch per photo (ARCHITECTURE MEDIUM, first pass).
/// </summary>
public sealed class GetCatProfileQueryHandler(
    ILogger<GetCatProfileQueryHandler> logger,
    CatEcommerceDbContext db,
    IStoredFileSigningBatcher signingBatcher)
    : IQueryHandlerAsync<GetCatProfileQuery, CatProfileViewModel?>
{
    public async Task<QueryResponse<CatProfileViewModel?>> HandleAsync(GetCatProfileQuery query)
    {
        logger.LogInformation("Reading profile for cat {CatId}", query.CatId);

        var projected = await db.Cats.AsNoTracking()
            .Where(c => c.Id == query.CatId)
            .Select(c => new
            {
                c.Id, c.OwnerId, c.Name, c.Species, c.Sex, c.DateOfBirth, c.DateOfBirthPrecision,
                c.Breed, c.ColourAndMarkings, c.NeuterStatus, c.NeuteredOn, c.IsHighRisk,
                c.HandlingNote, c.LifeStatus, c.DeceasedOn, c.DeceasedNote,
                c.DietaryNotes, c.BehaviouralNotes,
                HasRecordedAllergies = c.Allergies.Count > 0,
                Allergies = c.Allergies
                    .Select(a => new { a.Id, a.Allergen, a.Reaction, a.Severity, a.RecordedOn })
                    .ToList(),
                ChronicConditions = c.ChronicConditions
                    .Select(cc => new { cc.Id, cc.Condition, cc.DiagnosedOn, cc.Notes })
                    .ToList(),
                Medications = c.Medications
                    .Select(m => new { m.Id, m.MedicationName, m.Dosage, m.Frequency, m.StartedOn, m.EndsOn })
                    .ToList(),
                Photos = c.Photos
                    .Select(p => new { p.Id, p.FileId, p.IsPrimary, p.UploadedOn })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (projected is null)
            return new QueryResponse<CatProfileViewModel?> { Result = null };

        var latestWeightKg = await db.CatWeightRecords.AsNoTracking()
            .Where(w => w.CatId == query.CatId)
            .OrderByDescending(w => w.RecordedOn)
            .Select(w => (decimal?)w.Weight.Kilograms)
            .FirstOrDefaultAsync();

        var photoFileIds = projected.Photos.Select(p => p.FileId).ToList();
        var signedFiles = await signingBatcher.SignManyAsync(photoFileIds, FileServingRules.Inline);

        var photos = projected.Photos
            .Where(p => signedFiles.ContainsKey(p.FileId)) // a purged/erased file leaves no metadata to serve
            .Select(p =>
            {
                var signed = signedFiles[p.FileId];
                return new CatPhotoViewModel(p.Id, signed.FileId, signed.Url, signed.ContentType, signed.SizeInBytes, signed.OriginalFileName, signed.UrlExpiresOn, p.IsPrimary, p.UploadedOn);
            })
            .ToList();

        var viewModel = new CatProfileViewModel(
            projected.Id, projected.OwnerId, projected.Name, projected.Species, projected.Sex.DisplayName,
            projected.DateOfBirth, projected.DateOfBirthPrecision.DisplayName, projected.Breed,
            projected.ColourAndMarkings, projected.NeuterStatus.DisplayName, projected.NeuteredOn,
            projected.IsHighRisk, projected.HandlingNote, projected.LifeStatus.DisplayName,
            projected.DeceasedOn, projected.DeceasedNote, projected.DietaryNotes, projected.BehaviouralNotes,
            latestWeightKg,
            HasRecordedAllergies: projected.HasRecordedAllergies,
            Allergies: projected.Allergies.Select(a => new CatAllergyViewModel(
                a.Id, a.Allergen, a.Reaction, a.Severity.DisplayName, a.RecordedOn)).ToList(),
            ChronicConditions: projected.ChronicConditions.Select(c => new CatChronicConditionViewModel(
                c.Id, c.Condition, c.DiagnosedOn, c.Notes)).ToList(),
            Medications: projected.Medications.Select(m => new CatMedicationViewModel(
                m.Id, m.MedicationName, m.Dosage, m.Frequency, m.StartedOn, m.EndsOn)).ToList(),
            Photos: photos);

        return new QueryResponse<CatProfileViewModel?> { Result = viewModel };
    }
}
