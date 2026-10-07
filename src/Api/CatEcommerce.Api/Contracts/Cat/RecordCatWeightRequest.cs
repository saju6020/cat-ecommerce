namespace CatEcommerce.Api.Contracts.Cat;

/// <summary>
/// SECURITY gate, MEDIUM (2026-09-26): RecordedByStaffId is not client-suppliable -- it is always
/// the caller, taken from UserContext in CatsController, never trusted from the request body.
/// </summary>
public sealed class RecordCatWeightRequest
{
    public required decimal Kilograms { get; init; }
    public required DateOnly RecordedOn { get; init; }
    public string? Notes { get; init; }
}
