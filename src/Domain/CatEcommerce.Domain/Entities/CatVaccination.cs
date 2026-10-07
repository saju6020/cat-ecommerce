namespace CatEcommerce.Domain.Entities;

/// <summary>
/// IsCore and VaccineName are snapshots taken when the vaccination was recorded, not lookups —
/// a vaccine reclassified later must not retroactively change a historical clinical record.
/// HO-CAT-001 §8.3.
/// </summary>
public sealed class CatVaccination
{
    public Guid Id { get; private set; }
    public Guid VaccineId { get; private set; }
    public string VaccineCode { get; private set; } = default!;
    public string VaccineName { get; private set; } = default!;
    public bool IsCore { get; private set; }
    public string BatchReference { get; private set; } = default!;
    public DateOnly BatchExpiresOn { get; private set; }
    public DateOnly AdministeredOn { get; private set; }
    public Guid AdministeredByVetId { get; private set; }
    public DateOnly NextDueOn { get; private set; }
    public string? Notes { get; private set; }

    private CatVaccination() { }

    public CatVaccination(
        Guid id, Guid vaccineId, string vaccineCode, string vaccineName, bool isCore,
        string batchReference, DateOnly batchExpiresOn, DateOnly administeredOn,
        Guid administeredByVetId, DateOnly nextDueOn, string? notes)
    {
        Id = id;
        VaccineId = vaccineId;
        VaccineCode = vaccineCode;
        VaccineName = vaccineName;
        IsCore = isCore;
        BatchReference = batchReference;
        BatchExpiresOn = batchExpiresOn;
        AdministeredOn = administeredOn;
        AdministeredByVetId = administeredByVetId;
        NextDueOn = nextDueOn;
        Notes = notes;
    }
}
