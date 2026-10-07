using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Administration;

public sealed class CreateTaxClassCommand : Command
{
    public required Guid TaxClassId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
}

public sealed class UpdateTaxClassCommand : Command
{
    public required Guid TaxClassId { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class SetTaxRateCommand : Command
{
    public required Guid TaxRateId { get; init; }
    public required Guid TaxClassId { get; init; }
    public required decimal RatePercent { get; init; }
    public required DateTimeOffset EffectiveFrom { get; init; }
}
