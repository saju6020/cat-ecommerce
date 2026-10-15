using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Boarding;

/// <summary>HO-BRD-001 section 11.1 C1-C4. Ids are minted in the controller; <c>ValidateCommand = true</c> everywhere. Data only -- no orchestration.
/// Enumerations travel as their display names and are parsed after the validator has accepted them.</summary>
public sealed class CreateBoardingUnitCommand : Command
{
    public required Guid UnitId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required int CapacityCats { get; init; }
    public string? Block { get; init; }
    public int SortOrder { get; init; }
}

public sealed class UpdateBoardingUnitCommand : Command
{
    public required Guid UnitId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required int CapacityCats { get; init; }
    public string? Block { get; init; }
    public int SortOrder { get; init; }
}

public sealed class SetBoardingUnitActivationCommand : Command
{
    public required Guid UnitId { get; init; }
    public required Guid BranchId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class CreateMaintenanceBlockCommand : Command
{
    public required Guid BlockId { get; init; }
    public required Guid UnitId { get; init; }
    public required Guid BranchId { get; init; }
    public required DateOnly StartsOn { get; init; }
    public required DateOnly EndsOn { get; init; }
    public required string Reason { get; init; }
}

public sealed class CancelMaintenanceBlockCommand : Command
{
    public required Guid BlockId { get; init; }
    public required Guid UnitId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class SetBoardingRateCardCommand : Command
{
    public required Guid RateCardId { get; init; }
    public required Guid BranchId { get; init; }
    public required string UnitType { get; init; }
    public required decimal NightlyRate { get; init; }
    public required decimal WeeklyRate { get; init; }
    public required decimal MonthlyRate { get; init; }
    public decimal AdditionalCatNightlyRate { get; init; }
    public required Guid TaxClassId { get; init; }
    public required DateTimeOffset EffectiveFrom { get; init; }
}

public sealed record RoutineTimeItem(string Kind, TimeOnly Time);

public sealed class SetBoardingCareRoutineCommand : Command
{
    public required Guid BranchId { get; init; }
    public required IReadOnlyList<RoutineTimeItem> Times { get; init; }
}
