namespace CatEcommerce.Api.Contracts.Vaccination;

public sealed class RegisterVaccineRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required bool IsCore { get; init; }
    public required int ValidityMonths { get; init; }
    public string? Description { get; init; }
}
