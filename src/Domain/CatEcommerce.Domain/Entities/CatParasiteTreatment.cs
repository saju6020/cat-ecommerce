using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

public sealed class CatParasiteTreatment
{
    public Guid Id { get; private set; }
    public ParasiteTreatmentType TreatmentType { get; private set; } = default!;
    public string ProductName { get; private set; } = default!;
    public string? BatchReference { get; private set; }
    public DateOnly AdministeredOn { get; private set; }
    public Guid AdministeredByStaffId { get; private set; }
    public DateOnly NextDueOn { get; private set; }
    public string? Notes { get; private set; }

    private CatParasiteTreatment() { }

    public CatParasiteTreatment(
        Guid id, ParasiteTreatmentType treatmentType, string productName, string? batchReference,
        DateOnly administeredOn, Guid administeredByStaffId, DateOnly nextDueOn, string? notes)
    {
        Id = id;
        TreatmentType = treatmentType;
        ProductName = productName;
        BatchReference = batchReference;
        AdministeredOn = administeredOn;
        AdministeredByStaffId = administeredByStaffId;
        NextDueOn = nextDueOn;
        Notes = notes;
    }
}
