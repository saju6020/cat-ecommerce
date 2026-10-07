using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Cat;

public sealed class RecordCatVaccinationCommand : Command
{
    public required Guid CatId { get; init; }
    public required Guid VaccinationId { get; init; }
    public required Guid VaccineId { get; init; }
    public required string BatchReference { get; init; }
    public required DateOnly BatchExpiresOn { get; init; }
    public required DateOnly AdministeredOn { get; init; }
    public required Guid AdministeredByVetId { get; init; }
    public string? Notes { get; init; }
}
