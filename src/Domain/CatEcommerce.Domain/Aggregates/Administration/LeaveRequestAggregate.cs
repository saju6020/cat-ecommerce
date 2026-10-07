using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>HO-ADM-001 §8.4. Decider identity and self-approval rules are handler-level (they need
/// to know who the caller is and whether they are the sole administrator) -- this aggregate enforces
/// only what it can see from its own state.</summary>
public sealed class LeaveRequestAggregate : AggregateRoot
{
    public Guid StaffAccountId { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public LeaveKind Kind { get; private set; } = default!;
    public string? Note { get; private set; }
    public LeaveStatus Status { get; private set; } = default!;
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedOn { get; private set; }
    public string? DecisionNote { get; private set; }

    private LeaveRequestAggregate() { }

    public LeaveRequestAggregate(Guid id) : base(id) { }

    public void Request(LeaveRequestCreateDto dto)
    {
        if (dto.EndsOn < dto.StartsOn)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidLeavePeriod, "The end date cannot be before the start date.", nameof(dto.EndsOn));

        if (dto.EndsOn.DayNumber - dto.StartsOn.DayNumber + 1 > LeaveRules.MaxSpanDays)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidLeavePeriod, $"A leave request cannot span more than {LeaveRules.MaxSpanDays} days.", nameof(dto.EndsOn));

        if (dto.Note is not null && dto.Note.Length > LeaveRules.NoteMaxLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidLeavePeriod, "The note is too long.", nameof(dto.Note));

        StaffAccountId = dto.StaffAccountId;
        StartsOn = dto.StartsOn;
        EndsOn = dto.EndsOn;
        Kind = dto.Kind;
        Note = dto.Note;
        Status = LeaveStatus.Requested;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new LeaveRequestedEvent { LeaveRequestId = Id, StaffAccountId = StaffAccountId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Approve(LeaveDecisionDto dto)
    {
        EnsurePending();

        Status = LeaveStatus.Approved;
        DecidedBy = dto.DeciderId;
        DecidedOn = dto.DecidedOn;
        DecisionNote = dto.DecisionNote;

        AddDomainEvent(new StaffLeaveApprovedEvent
        {
            LeaveRequestId = Id, StaffAccountId = StaffAccountId, StartsOn = StartsOn, EndsOn = EndsOn,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    public void Reject(LeaveDecisionDto dto)
    {
        EnsurePending();

        Status = LeaveStatus.Rejected;
        DecidedBy = dto.DeciderId;
        DecidedOn = dto.DecidedOn;
        DecisionNote = dto.DecisionNote;

        AddDomainEvent(new LeaveRejectedEvent { LeaveRequestId = Id, StaffAccountId = StaffAccountId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Cancel(LeaveCancelDto dto)
    {
        if (Status == LeaveStatus.Approved && EndsOn < dto.Today)
            throw new DomainRuleViolationException(BusinessViolationCodes.LeaveCannotBeCancelled, "An approved leave that has already ended cannot be cancelled.", nameof(EndsOn));

        if (Status != LeaveStatus.Requested && Status != LeaveStatus.Approved)
            throw new DomainRuleViolationException(BusinessViolationCodes.LeaveCannotBeCancelled, "Only a requested or approved leave can be cancelled.", nameof(Status));

        var wasApproved = Status == LeaveStatus.Approved;
        Status = LeaveStatus.Cancelled;

        AddDomainEvent(new LeaveCancelledEvent { LeaveRequestId = Id, StaffAccountId = StaffAccountId, WasApproved = wasApproved, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    private void EnsurePending()
    {
        if (Status != LeaveStatus.Requested)
            throw new DomainRuleViolationException(BusinessViolationCodes.LeaveNotPending, "This leave request has already been decided.", nameof(Status));
    }
}
