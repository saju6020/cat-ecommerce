using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Boarding;

/// <summary>
/// HO-BRD-001 section 8.13 (US-BRD-13, ADR-226 as renumbered ADR-359) -- something that happened to a boarded cat: injury, illness, escape attempt, fight, refusal to eat.
/// Its OWN root. Medium and high incidents notify the owner (high as an Urgent template, FR-NTF-7); an unacknowledged high incident past
/// <c>boarding.incidentAckEscalationMinutes</c> is ESCALATED by the worker to a phone-call follow-up, which is state on the incident -- not a task (a second aggregate).
/// </summary>
public sealed class BoardingIncidentAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public Guid StayId { get; private set; }
    public Guid CatId { get; private set; }
    public IncidentKind Kind { get; private set; } = default!;
    public IncidentSeverity Severity { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string ImmediateAction { get; private set; } = default!;
    public DateTimeOffset OccurredOn { get; private set; }
    public Guid ReportedBy { get; private set; }
    public DateTimeOffset ReportedOn { get; private set; }
    public bool OwnerNotified { get; private set; }
    public DateTimeOffset? OwnerAcknowledgedOn { get; private set; }
    public AcknowledgementChannel? AcknowledgedVia { get; private set; }
    public DateTimeOffset? EscalatedOn { get; private set; }
    public DateTimeOffset? PhoneCallOn { get; private set; }
    public Guid? PhoneCallBy { get; private set; }
    public string? PhoneCallOutcome { get; private set; }
    public IncidentStatus Status { get; private set; } = default!;
    public DateTimeOffset? ResolvedOn { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public string? Resolution { get; private set; }

    private BoardingIncidentAggregate() { }

    public BoardingIncidentAggregate(Guid id) : base(id) { }

    public void Record(
        Guid branchId, Guid stayId, Guid catId, IncidentKind kind, IncidentSeverity severity, string description, string immediateAction, DateTimeOffset occurredOn,
        DateTimeOffset now, UserContext userContext, Guid correlationId)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > IncidentRules.MaxDescriptionLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidIncident, "A description is required (up to 2000 characters).", nameof(description));
        if (string.IsNullOrWhiteSpace(immediateAction) || immediateAction.Trim().Length > IncidentRules.MaxImmediateActionLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidIncident, "The immediate action taken is required (up to 2000 characters).", nameof(immediateAction));
        if (occurredOn > now.AddMinutes(5))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidIncident, "An incident cannot be in the future.", nameof(occurredOn));

        BranchId = branchId;
        StayId = stayId;
        CatId = catId;
        Kind = kind;
        Severity = severity;
        Description = description.Trim();
        ImmediateAction = immediateAction.Trim();
        OccurredOn = occurredOn;
        ReportedBy = userContext.UserId;
        ReportedOn = now;
        OwnerNotified = severity != IncidentSeverity.Low;
        Status = IncidentStatus.Open;

        SetDefaultValue(userContext);
        AddDomainEvent(new IncidentRecordedEvent { IncidentId = Id, StayId = StayId, CatId = CatId, Severity = Severity, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    /// <summary>The owner (portal) or staff (the owner phoned) acknowledged it. Idempotent: the first acknowledgement stands.</summary>
    public void Acknowledge(DateTimeOffset now, AcknowledgementChannel channel)
    {
        if (OwnerAcknowledgedOn is not null) return;

        OwnerAcknowledgedOn = now;
        AcknowledgedVia = channel;
        UpdateVersion();
    }

    /// <summary>Worker only: high, owner notified, unacknowledged for the configured minutes, not yet escalated. Anything else is a no-op.</summary>
    public void Escalate(DateTimeOffset now, int escalationMinutes, UserContext userContext, Guid correlationId)
    {
        if (Severity != IncidentSeverity.High || !OwnerNotified || OwnerAcknowledgedOn is not null || EscalatedOn is not null) return;
        if (Status != IncidentStatus.Open || now < ReportedOn.AddMinutes(escalationMinutes)) return;

        EscalatedOn = now;
        AddDomainEvent(new IncidentEscalatedEvent { IncidentId = Id, StayId = StayId, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    /// <summary>Closes the follow-up. When the owner was reached the call counts as an acknowledgement.</summary>
    public void RecordPhoneCall(DateTimeOffset now, string outcome, bool ownerReached, UserContext userContext)
    {
        if (string.IsNullOrWhiteSpace(outcome) || outcome.Trim().Length > IncidentRules.MaxPhoneOutcomeLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.PhoneCallOutcomeRequired, "The outcome of the call is required.", nameof(outcome));

        PhoneCallOn = now;
        PhoneCallBy = userContext.UserId;
        PhoneCallOutcome = outcome.Trim();
        if (ownerReached) Acknowledge(now, AcknowledgementChannel.Staff);
        UpdateVersion();
    }

    public void Resolve(DateTimeOffset now, string resolution, UserContext userContext)
    {
        if (Status == IncidentStatus.Resolved) return;
        if (string.IsNullOrWhiteSpace(resolution) || resolution.Trim().Length > IncidentRules.MaxResolutionLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidIncident, "A resolution is required (up to 1000 characters).", nameof(resolution));

        Status = IncidentStatus.Resolved;
        ResolvedOn = now;
        ResolvedBy = userContext.UserId;
        Resolution = resolution.Trim();
        UpdateVersion();
    }

    /// <summary>On the dashboard's "Call owner" list: escalated and nobody has phoned yet.</summary>
    public bool NeedsPhoneCall => EscalatedOn is not null && PhoneCallOn is null && Status == IncidentStatus.Open;
}
