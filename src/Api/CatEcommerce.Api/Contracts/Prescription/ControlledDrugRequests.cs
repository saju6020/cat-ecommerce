namespace CatEcommerce.Api.Contracts.Prescription;

/// <summary><c>POST /api/staff/controlled-drugs/register/{entryId}/corrections</c> (C7). The route's <c>entryId</c> is the entry being corrected; the new entry's id is
/// minted by the controller. The witness enters their OWN identifier and password (ADR-146) -- there is no witness picker.</summary>
public sealed class RecordCorrectionRequest
{
    public string? CorrectedDoseText { get; init; }
    public Guid? CorrectedCatId { get; init; }
    public decimal QuantityDelta { get; init; }
    public string? Notes { get; init; }
    public string Reason { get; init; } = "";
    public string WitnessIdentifier { get; init; } = "";
    public string WitnessPassword { get; init; } = "";
}
