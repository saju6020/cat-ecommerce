namespace CatEcommerce.Api.Contracts.Cat;

public sealed class RecordCatParasiteTreatmentRequest
{
    public required string TreatmentType { get; init; }
    public required string ProductName { get; init; }
    public string? BatchReference { get; init; }
    public required DateOnly AdministeredOn { get; init; }
    public required Guid AdministeredByStaffId { get; init; }
    public required int IntervalDays { get; init; }
    public string? Notes { get; init; }
}
