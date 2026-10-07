using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Vaccination;

public sealed class RegisterVaccineCommand : Command
{
    public required Guid VaccineId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required bool IsCore { get; init; }
    public required int ValidityMonths { get; init; }
    public string? Description { get; init; }
}
