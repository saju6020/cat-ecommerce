namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-PHM-001 §8.7 (ADR-147) -- the medicine label frozen at dispensing. A reprint prints this same snapshot: a label never changes after the
/// medicine leaves the counter. Cat name, owner name, medicine, dose line, instructions, date and (earliest) expiry are US-PHM-05 AC1's fields.
/// </summary>
public sealed record MedicineLabelSnapshot
{
    public string CatName { get; init; }
    public string OwnerName { get; init; }
    public string MedicineLine { get; init; }
    public string DoseLine { get; init; }
    public string Instructions { get; init; }
    public DateOnly DispensedOnLocal { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    public string QuantityLine { get; init; }
    public string PrescriptionNumber { get; init; }
    public string PrescriberName { get; init; }
    public string ClinicName { get; init; }
    public string ClinicPhone { get; init; }
    public string? ClinicAddress { get; init; }
    public string? ControlledNotice { get; init; }
    public string StandardWarnings { get; init; }

    public MedicineLabelSnapshot(
        string catName, string ownerName, string medicineLine, string doseLine, string instructions, DateOnly dispensedOnLocal, DateOnly? expiresOn,
        string quantityLine, string prescriptionNumber, string prescriberName, string clinicName, string clinicPhone, string? clinicAddress,
        string? controlledNotice, string standardWarnings)
    {
        CatName = catName;
        OwnerName = ownerName;
        MedicineLine = medicineLine;
        DoseLine = doseLine;
        Instructions = instructions;
        DispensedOnLocal = dispensedOnLocal;
        ExpiresOn = expiresOn;
        QuantityLine = quantityLine;
        PrescriptionNumber = prescriptionNumber;
        PrescriberName = prescriberName;
        ClinicName = clinicName;
        ClinicPhone = clinicPhone;
        ClinicAddress = clinicAddress;
        ControlledNotice = controlledNotice;
        StandardWarnings = standardWarnings;
    }

    // EF only.
    private MedicineLabelSnapshot()
    {
        CatName = OwnerName = MedicineLine = DoseLine = Instructions = QuantityLine = PrescriptionNumber = PrescriberName = ClinicName = ClinicPhone = StandardWarnings = default!;
    }
}
