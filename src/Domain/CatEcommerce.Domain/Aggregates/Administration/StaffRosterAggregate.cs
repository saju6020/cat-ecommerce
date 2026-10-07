using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>
/// HO-ADM-001 §8.4 / ADR-047. One per (StaffAccountId, BranchId). <see cref="PromoteDuePattern"/> is
/// called lazily by any command that loads this roster and by the availability calculator on read --
/// never by a background job (ADR-047, §20.8).
/// </summary>
public sealed class StaffRosterAggregate : AggregateRoot
{
    private readonly List<ShiftSlot> _currentPattern = [];
    private readonly List<ShiftSlot> _nextPattern = [];

    public Guid StaffAccountId { get; private set; }
    public Guid BranchId { get; private set; }
    public IReadOnlyList<ShiftSlot> CurrentPattern => _currentPattern;

    /// <summary>Empty when no pattern is scheduled -- check <see cref="NextPatternEffectiveFrom"/>
    /// for "is one scheduled", not this list's emptiness (an EF-owned collection cannot be
    /// modelled as nullable).</summary>
    public IReadOnlyList<ShiftSlot> NextPattern => _nextPattern;
    public DateOnly? NextPatternEffectiveFrom { get; private set; }

    private StaffRosterAggregate() { }

    public StaffRosterAggregate(Guid id) : base(id) { }

    /// <summary>Load-or-create: called by the handler when no roster exists yet for the pair.</summary>
    public void Open(Guid staffAccountId, Guid branchId, Platform.Infrastructure.Common.Security.UserContext userContext)
    {
        StaffAccountId = staffAccountId;
        BranchId = branchId;
        SetDefaultValue(userContext);
    }

    public void SetCurrentPattern(RosterCurrentPatternSetDto dto)
    {
        ValidateSlots(dto.Slots);

        _currentPattern.Clear();
        foreach (var slot in dto.Slots)
            _currentPattern.Add(new ShiftSlot(Guid.NewGuid(), slot.DayOfWeek, slot.Starts, slot.Ends, slot.DefaultRoomId));

        AddDomainEvent(new StaffRosterPatternChangedEvent
        {
            StaffAccountId = StaffAccountId, BranchId = BranchId, EffectiveFrom = null,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    public void ScheduleNextPattern(RosterNextPatternScheduleDto dto)
    {
        if (dto.EffectiveFrom <= dto.Today)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidPatternEffectiveDate,
                "The next pattern's effective date must be strictly after today.", nameof(dto.EffectiveFrom));

        ValidateSlots(dto.Slots);

        _nextPattern.Clear();
        foreach (var slot in dto.Slots)
            _nextPattern.Add(new ShiftSlot(Guid.NewGuid(), slot.DayOfWeek, slot.Starts, slot.Ends, slot.DefaultRoomId));
        NextPatternEffectiveFrom = dto.EffectiveFrom;

        AddDomainEvent(new StaffRosterPatternChangedEvent
        {
            StaffAccountId = StaffAccountId, BranchId = BranchId, EffectiveFrom = dto.EffectiveFrom,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    public void CancelNextPattern(RosterNextPatternCancelDto dto)
    {
        if (_nextPattern.Count == 0 && NextPatternEffectiveFrom is null) return;

        _nextPattern.Clear();
        NextPatternEffectiveFrom = null;

        AddDomainEvent(new StaffRosterPatternChangedEvent
        {
            StaffAccountId = StaffAccountId, BranchId = BranchId, EffectiveFrom = null,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    /// <summary>If the scheduled pattern is due, current becomes next and next is cleared. Raises
    /// an event only when a promotion actually happens.</summary>
    public void PromoteDuePattern(RosterPatternPromoteDto dto)
    {
        if (NextPatternEffectiveFrom is null || NextPatternEffectiveFrom > dto.AsOf) return;

        _currentPattern.Clear();
        _currentPattern.AddRange(_nextPattern.Select(s => new ShiftSlot(Guid.NewGuid(), s.DayOfWeek, s.Starts, s.Ends, s.DefaultRoomId)));
        _nextPattern.Clear();
        NextPatternEffectiveFrom = null;

        AddDomainEvent(new StaffRosterPatternChangedEvent
        {
            StaffAccountId = StaffAccountId, BranchId = BranchId, EffectiveFrom = null,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    private static void ValidateSlots(IReadOnlyList<ShiftSlotInput> slots)
    {
        foreach (var group in slots.GroupBy(s => s.DayOfWeek))
        {
            if (group.Count() > StaffRosterRules.MaxSlotsPerDay)
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidShiftSlot, "No more than 3 shifts per day are allowed.", nameof(slots));

            var ordered = group.OrderBy(s => s.Starts).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Starts >= ordered[i].Ends)
                    throw new DomainRuleViolationException(BusinessViolationCodes.InvalidShiftSlot, "A shift's start must be before its end.", nameof(slots));

                if (i > 0 && ordered[i].Starts < ordered[i - 1].Ends)
                    throw new DomainRuleViolationException(BusinessViolationCodes.InvalidShiftSlot, "Shifts on the same day must not overlap.", nameof(slots));
            }
        }
    }
}
