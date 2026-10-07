using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>HO-ADM-001 §8.4. Unbounded over time -- its own root.</summary>
public sealed class RosterExceptionAggregate : AggregateRoot
{
    public Guid StaffAccountId { get; private set; }
    public Guid BranchId { get; private set; }
    public DateOnly Date { get; private set; }
    public RosterExceptionKind Kind { get; private set; } = default!;
    public TimeOnly Starts { get; private set; }
    public TimeOnly Ends { get; private set; }
    public string Reason { get; private set; } = default!;
    public bool IsCancelled { get; private set; }

    private RosterExceptionAggregate() { }

    public RosterExceptionAggregate(Guid id) : base(id) { }

    public void Create(RosterExceptionCreateDto dto)
    {
        if (dto.Starts >= dto.Ends)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidShiftSlot, "A start must be before its end.", nameof(dto.Starts));
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 200)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidShiftSlot, "A reason is required.", nameof(dto.Reason));

        StaffAccountId = dto.StaffAccountId;
        BranchId = dto.BranchId;
        Date = dto.Date;
        Kind = dto.Kind;
        Starts = dto.Starts;
        Ends = dto.Ends;
        Reason = dto.Reason;
        IsCancelled = false;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new RosterExceptionCreatedEvent { RosterExceptionId = Id, StaffAccountId = StaffAccountId, BranchId = BranchId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Cancel(RosterExceptionCancelDto dto)
    {
        if (Date <= dto.Today)
            throw new DomainRuleViolationException(BusinessViolationCodes.PastClosureCannotBeCancelled, "A past roster exception cannot be cancelled.", nameof(Date));

        IsCancelled = true;

        AddDomainEvent(new RosterExceptionCancelledEvent { RosterExceptionId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }
}
