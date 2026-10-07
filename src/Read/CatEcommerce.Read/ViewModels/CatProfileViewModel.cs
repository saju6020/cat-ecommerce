namespace CatEcommerce.Read.ViewModels;

/// <summary>Full profile -- GET /api/cats/{catId}, Q1 (HO-CAT-001 section 12.3).</summary>
public sealed record CatProfileViewModel(
    Guid Id,
    Guid OwnerId,
    string Name,
    string Species,
    string Sex,
    DateOnly? DateOfBirth,
    string DateOfBirthPrecision,
    string? Breed,
    string? ColourAndMarkings,
    string NeuterStatus,
    DateOnly? NeuteredOn,
    bool IsHighRisk,
    string? HandlingNote,
    string LifeStatus,
    DateOnly? DeceasedOn,
    string? DeceasedNote,
    string? DietaryNotes,
    string? BehaviouralNotes,
    decimal? LatestWeightKg,
    bool HasRecordedAllergies,
    IReadOnlyList<CatAllergyViewModel> Allergies,
    IReadOnlyList<CatChronicConditionViewModel> ChronicConditions,
    IReadOnlyList<CatMedicationViewModel> Medications,
    IReadOnlyList<CatPhotoViewModel> Photos);

public sealed record CatAllergyViewModel(
    Guid Id, string Allergen, string Reaction, string Severity, DateOnly RecordedOn);

public sealed record CatChronicConditionViewModel(
    Guid Id, string Condition, DateOnly? DiagnosedOn, string? Notes);

public sealed record CatMedicationViewModel(
    Guid Id, string MedicationName, string Dosage, string Frequency, DateOnly StartedOn, DateOnly? EndsOn);

/// <summary>Gate-fix 2026-09-27 (ARCHITECTURE HIGH): the §12.4 signed-file shape
/// (<c>{ fileId, url, contentType, sizeInBytes, originalFileName, urlExpiresOn }</c>) plus
/// <see cref="IsPrimary"/> -- serving now goes through FIL's signed URL by file id, never a raw
/// storage key.
///
/// Gate-fix 2026-09-27 (ARCHITECTURE MEDIUM, second pass): <see cref="Id"/> (the photo id,
/// the same value <c>POST /api/cats/{id}/photos</c> already returns as <c>photoId</c>) and
/// <see cref="UploadedOn"/> restored -- an earlier version of this shape dropped both when it
/// switched from a raw storage key to FIL's signed §12.4 shape, leaving the POST's own <c>photoId</c>
/// unreachable from any read.</summary>
public sealed record CatPhotoViewModel(
    Guid Id, Guid FileId, string Url, string ContentType, long SizeInBytes, string OriginalFileName,
    DateTimeOffset UrlExpiresOn, bool IsPrimary, DateTimeOffset UploadedOn);
