namespace CatEcommerce.Read.ViewModels;

// HO-BRD-001 section 12.4 -- units, maintenance, rate cards and the care routine. Money is MoneyViewModel (amount as a string, HO-ADM-001 ADR-054);
// instants carry an offset; local dates are yyyy-MM-dd; local times are "HH:mm" strings.

/// <summary><paramref name="NightlyRate"/> is the unit TYPE's rate card in force (ADR-221): <c>null</c> when the type has no current card (not bookable).</summary>
public sealed record BoardingUnitViewModel(
    Guid Id, Guid BranchId, string Code, string Name, string UnitType, int CapacityCats, string? Block, bool IsActive, int SortOrder,
    MoneyViewModel? NightlyRate, int Version);

public sealed record MaintenanceBlockViewModel(Guid Id, Guid UnitId, DateOnly StartsOn, DateOnly EndsOn, string Reason, bool IsCancelled);

/// <summary><paramref name="Status"/> is derived at read time: <c>Current</c> (the one in force), <c>Scheduled</c> (effective later) or <c>Past</c>.</summary>
public sealed record BoardingRateCardViewModel(
    Guid Id, string UnitType, MoneyViewModel NightlyRate, MoneyViewModel WeeklyRate, MoneyViewModel MonthlyRate, MoneyViewModel AdditionalCatNightlyRate,
    Guid TaxClassId, DateTimeOffset EffectiveFrom, Guid SetBy, string Status);

public sealed record RoutineTimeViewModel(string Kind, string Time);

/// <summary><paramref name="IsDefault"/> is true while the branch has set no routine of its own and the shipped defaults apply.</summary>
public sealed record BoardingCareRoutineViewModel(Guid BranchId, bool IsDefault, IReadOnlyList<RoutineTimeViewModel> Times);

/// <summary>The public price list: a type is listed only when it has a rate card in force and an active unit.</summary>
public sealed record BoardingUnitTypeViewModel(
    string UnitType, int CapacityCats, MoneyViewModel NightlyRate, MoneyViewModel WeeklyRate, MoneyViewModel MonthlyRate, MoneyViewModel AdditionalCatNightlyRate);
