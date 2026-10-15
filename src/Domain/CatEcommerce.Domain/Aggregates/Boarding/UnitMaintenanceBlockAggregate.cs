using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Boarding;

/// <summary>
/// HO-BRD-001 section 8.2 (US-BRD-01 AC2). A maintenance block takes one unit out of availability for <c>[StartsOn at 00:00, EndsOn + 1 at 00:00)</c>
/// local. The instant range (<see cref="OccupiedFrom"/>/<see cref="OccupiedTo"/>) is what the exclusion constraint <c>ex_unit_maintenance</c> reads
/// (F31: a constraint sees only its own row's columns). A block overlapping an active allocation is refused by the handler (F32); the constraint
/// stops two blocks on one unit.
/// </summary>
public sealed class UnitMaintenanceBlockAggregate : AggregateRoot
{
    public Guid UnitId { get; private set; }
    public Guid BranchId { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public string Reason { get; private set; } = default!;
    public DateTimeOffset OccupiedFrom { get; private set; }
    public DateTimeOffset OccupiedTo { get; private set; }
    public bool IsCancelled { get; private set; }

    private UnitMaintenanceBlockAggregate() { }

    public UnitMaintenanceBlockAggregate(Guid id) : base(id) { }

    public void Create(MaintenanceBlockCreateDto dto)
    {
        if (dto.EndsOn < dto.StartsOn)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidMaintenanceBlock, "A maintenance block cannot end before it starts.", nameof(dto.EndsOn));

        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length > StayRules.MaxMaintenanceReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidMaintenanceBlock, $"A reason is required and cannot exceed {StayRules.MaxMaintenanceReasonLength} characters.", nameof(dto.Reason));

        UnitId = dto.UnitId;
        BranchId = dto.BranchId;
        StartsOn = dto.StartsOn;
        EndsOn = dto.EndsOn;
        Reason = dto.Reason.Trim();
        OccupiedFrom = dto.OccupiedFrom;
        OccupiedTo = dto.OccupiedTo;
        IsCancelled = false;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new UnitMaintenanceBlockCreatedEvent { BlockId = Id, UnitId = UnitId, StartsOn = StartsOn, EndsOn = EndsOn, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>Cancellable until its last day has passed; cancelling twice is a no-op.</summary>
    public void Cancel(MaintenanceBlockCancelDto dto)
    {
        if (IsCancelled) return;

        if (dto.Today > EndsOn)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidMaintenanceBlock, "A maintenance block that has already finished cannot be cancelled.", nameof(EndsOn));

        IsCancelled = true;
        AddDomainEvent(new UnitMaintenanceBlockCancelledEvent { BlockId = Id, UnitId = UnitId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }
}
