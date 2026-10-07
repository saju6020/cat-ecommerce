namespace CatEcommerce.Api.Contracts.Cat;

public sealed class RecordCatVaccinationRequest
{
    public required Guid VaccineId { get; init; }
    public required string BatchReference { get; init; }
    public required DateOnly BatchExpiresOn { get; init; }
    public required DateOnly AdministeredOn { get; init; }
    public required Guid AdministeredByVetId { get; init; }
    public string? Notes { get; init; }
}
