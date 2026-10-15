namespace CatEcommerce.Api.Contracts.Prescription;

public sealed class DispenseLineRequest
{
    public Guid SkuId { get; init; }
    public Guid BatchId { get; init; }
    public decimal Quantity { get; init; }
}

/// <summary><c>POST /api/staff/prescriptions/{prescriptionId}/dispensings</c> (C9). The lines name the EXACT batches handed over; the server's suggestion is the FEFO plan. A
/// controlled medicine's witness enters their OWN identifier and password (ADR-146) -- there is no witness picker.</summary>
public sealed class DispensePrescriptionRequest
{
    public List<DispenseLineRequest> Lines { get; init; } = [];
    public string? FefoOverrideReason { get; init; }
    public Guid? RepeatRequestId { get; init; }
    public string? WitnessIdentifier { get; init; }
    public string? WitnessPassword { get; init; }
}

/// <summary><c>POST /api/staff/dispensings/{dispensingId}/reverse</c> (C11).</summary>
public sealed class ReverseDispensingRequest
{
    public string Reason { get; init; } = "";
    public string? WitnessIdentifier { get; init; }
    public string? WitnessPassword { get; init; }
}
