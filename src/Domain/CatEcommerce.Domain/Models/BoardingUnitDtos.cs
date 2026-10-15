using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

// HO-BRD-001 section 11.3 -- the immutable inputs of the units, maintenance, rate-card and routine aggregates.

public sealed record BoardingUnitCreateDto(
    Guid BranchId, string Code, string Name, BoardingUnitType Type, int CapacityCats, string? Block, int SortOrder,
    UserContext UserContext, Guid CorrelationId);

/// <summary><paramref name="HasFutureAllocations"/> is read by the handler from the stays; the rule that a unit with future stays keeps its type is the aggregate's.</summary>
public sealed record BoardingUnitUpdateDto(
    string Name, BoardingUnitType Type, int CapacityCats, string? Block, int SortOrder, bool HasFutureAllocations,
    UserContext UserContext, Guid CorrelationId);

public sealed record BoardingUnitActivationDto(bool IsActive, bool HasFutureAllocations, UserContext UserContext, Guid CorrelationId);

/// <summary><paramref name="OccupiedFrom"/>/<paramref name="OccupiedTo"/> are the local days converted to instants by <c>BoardingOccupancyWindow.ForDays</c> (the constraint's range).</summary>
public sealed record MaintenanceBlockCreateDto(
    Guid UnitId, Guid BranchId, DateOnly StartsOn, DateOnly EndsOn, string Reason, DateTimeOffset OccupiedFrom, DateTimeOffset OccupiedTo,
    UserContext UserContext, Guid CorrelationId);

public sealed record MaintenanceBlockCancelDto(DateOnly Today, UserContext UserContext, Guid CorrelationId);

public sealed record BoardingRateCardCreateDto(
    Guid BranchId, BoardingUnitType UnitType, decimal NightlyRate, decimal WeeklyRate, decimal MonthlyRate, decimal AdditionalCatNightlyRate,
    Guid TaxClassId, string CurrencyCode, DateTimeOffset EffectiveFrom, DateTimeOffset Now, Guid SetBy,
    UserContext UserContext, Guid CorrelationId);

public sealed record RoutineTimeInput(RoutineTaskKind Kind, TimeOnly Time);

public sealed record BoardingCareRoutineDto(Guid BranchId, IReadOnlyList<RoutineTimeInput> Times, UserContext UserContext, Guid CorrelationId);
