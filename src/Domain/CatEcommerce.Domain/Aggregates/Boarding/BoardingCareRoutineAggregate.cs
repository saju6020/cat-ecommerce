using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Boarding;

/// <summary>
/// HO-BRD-001 section 8.2 (US-BRD-09). A branch's care routine: the local times litter changes, enrichment and observation checks are scheduled. The
/// aggregate's id IS the branch id (one per branch, created on first write). Until it exists the branch uses <see cref="BoardingRoutineDefaults"/>.
/// </summary>
public sealed class BoardingCareRoutineAggregate : AggregateRoot
{
    private readonly List<RoutineTime> _times = [];

    public IReadOnlyList<RoutineTime> Times => _times;

    private BoardingCareRoutineAggregate() { }

    public BoardingCareRoutineAggregate(Guid branchId) : base(branchId) { }

    public Guid BranchId => Id;

    /// <summary>Replaces every time. At most <see cref="BoardingRoutineDefaults.MaxTimesPerKind"/> per kind, no duplicate (kind, time); stored sorted.</summary>
    public void SetTimes(BoardingCareRoutineDto dto)
    {
        if (dto.Times.Count > BoardingRoutineDefaults.MaxTimes)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidCareRoutine, $"A routine has at most {BoardingRoutineDefaults.MaxTimes} times.", nameof(dto.Times));

        foreach (var group in dto.Times.GroupBy(t => t.Kind))
        {
            if (group.Count() > BoardingRoutineDefaults.MaxTimesPerKind)
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidCareRoutine, $"A routine has at most {BoardingRoutineDefaults.MaxTimesPerKind} times per kind.", nameof(dto.Times));

            if (group.Select(t => t.Time).Distinct().Count() != group.Count())
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidCareRoutine, "The same time cannot be listed twice for one kind.", nameof(dto.Times));
        }

        _times.Clear();
        _times.AddRange(dto.Times
            .OrderBy(t => t.Kind.Value).ThenBy(t => t.Time)
            .Select(t => new RoutineTime(Guid.NewGuid(), t.Kind, t.Time)));

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new BoardingCareRoutineSetEvent { BranchId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>The routine in force: the branch's own times, or the shipped defaults when it has none.</summary>
    public static IReadOnlyList<RoutineTimeInput> Effective(BoardingCareRoutineAggregate? routine) =>
        Effective(routine?.Times.Select(t => new RoutineTimeInput(t.Kind, t.LocalTime)).ToList());

    /// <summary>The same rule over plain values, so a read can project the times and still apply the defaults (the aggregate overload delegates here).</summary>
    public static IReadOnlyList<RoutineTimeInput> Effective(IReadOnlyList<RoutineTimeInput>? times) =>
        times is { Count: > 0 }
            ? times
            : BoardingRoutineDefaults.Times
                .Select(t => new RoutineTimeInput(Enumeration.FromDisplayName<RoutineTaskKind>(t.Kind), t.Time)).ToList();
}
