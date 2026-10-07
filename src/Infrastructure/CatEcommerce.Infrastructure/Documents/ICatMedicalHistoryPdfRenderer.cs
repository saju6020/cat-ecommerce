namespace CatEcommerce.Infrastructure.Documents;

/// <summary>
/// US-CAT-10 (HO-CAT-001 section 12.2). Behind an interface so the Api layer never references
/// QuestPDF directly, and the renderer can be swapped without touching a controller.
///
/// Takes <see cref="CatMedicalHistoryDocument"/> rather than
/// <c>CatEcommerce.Read.ViewModels.CatMedicalHistoryViewModel</c> (as the handover's prose
/// literally says): <c>CatEcommerce.Read</c> already references <c>CatEcommerce.Infrastructure</c>
/// (for <c>CatEcommerceDbContext</c>), so the reverse reference this interface's signature would
/// require is circular and does not compile. CatsController (which references both Read and
/// Infrastructure) maps the query result into this document type before calling the renderer.
/// </summary>
public interface ICatMedicalHistoryPdfRenderer
{
    byte[] Render(CatMedicalHistoryDocument document);
}

public sealed record CatMedicalHistoryDocument(
    Guid CatId,
    string Name,
    Guid OwnerId,
    string Species,
    string? Breed,
    string Sex,
    DateOnly? DateOfBirth,
    string DateOfBirthPrecision,
    string NeuterStatus,
    DateOnly? NeuteredOn,
    bool IsHighRisk,
    string? HandlingNote,
    IReadOnlyList<CatMedicalHistoryDocumentVaccination> Vaccinations,
    IReadOnlyList<CatMedicalHistoryDocumentParasiteTreatment> ParasiteTreatments,
    IReadOnlyList<CatMedicalHistoryDocumentWeightPoint> WeightHistory);

public sealed record CatMedicalHistoryDocumentVaccination(
    string VaccineName, string BatchReference, DateOnly AdministeredOn, DateOnly NextDueOn);

public sealed record CatMedicalHistoryDocumentParasiteTreatment(
    string TreatmentType, string ProductName, DateOnly AdministeredOn, DateOnly NextDueOn);

public sealed record CatMedicalHistoryDocumentWeightPoint(DateOnly RecordedOn, decimal Kilograms);
