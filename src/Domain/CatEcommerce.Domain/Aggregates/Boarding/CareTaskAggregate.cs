using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Boarding;

/// <summary>
/// HO-BRD-001 section 8.9 (US-BRD-09, US-BRD-10, ADR-224 as renumbered ADR-357) -- one thing a caretaker does for one cat on one local date. Its OWN root (rule 3: it grows
/// with days times cats). Generated idempotently by the unique key <c>(stay, cat, local date, source key)</c>. Completion needs no body and a second tap is a no-op.
/// <b>Overdue is derived</b> (<see cref="IsOverdue"/>), never stored. A voided task frees its source key (suffixed with its id) so the profile's new schedule can regenerate it.
/// </summary>
public sealed class CareTaskAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public Guid StayId { get; private set; }
    public Guid CatId { get; private set; }
    public Guid UnitId { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public CareTaskKind Kind { get; private set; } = default!;
    public DateTimeOffset DueOn { get; private set; }
    public TimeOnly DueLocalTime { get; private set; }
    public string SourceKey { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public Guid? AssignedCaretakerId { get; private set; }
    public CareTaskStatus Status { get; private set; } = default!;
    public DateTimeOffset? CompletedOn { get; private set; }
    public Guid? CompletedBy { get; private set; }
    public string? Note { get; private set; }
    public string? SkipReason { get; private set; }

    private CareTaskAggregate() { }

    public CareTaskAggregate(Guid id) : base(id) { }

    public void Create(Guid branchId, Guid stayId, DateOnly localDate, CareTaskSeed seed, Guid? assignedCaretakerId, UserContext userContext)
    {
        BranchId = branchId;
        StayId = stayId;
        CatId = seed.CatId;
        UnitId = seed.UnitId;
        LocalDate = localDate;
        Kind = seed.Kind;
        DueOn = seed.DueOn;
        DueLocalTime = seed.DueLocalTime;
        SourceKey = seed.SourceKey;
        Title = seed.Title;
        AssignedCaretakerId = assignedCaretakerId;
        Status = CareTaskStatus.Pending;

        SetDefaultValue(userContext);
        UpdateVersion();
    }

    /// <summary>Pending -> Done with the time and the caller. Completing a Done task changes nothing (a double tap is harmless).</summary>
    public void Complete(Guid by, DateTimeOffset now, string? note, UserContext userContext, Guid correlationId)
    {
        if (Status == CareTaskStatus.Done) return;
        if (Status != CareTaskStatus.Pending)
            throw new DomainRuleViolationException(BusinessViolationCodes.CareTaskNotPending, "This task is no longer pending.", nameof(Status));
        if (note is { Length: > IncidentRules.MaxTaskNoteLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.CareTaskNotPending, "The note is too long.", nameof(note));

        Status = CareTaskStatus.Done;
        CompletedOn = now;
        CompletedBy = by;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        AddDomainEvent(new CareTaskCompletedEvent { TaskId = Id, StayId = StayId, CatId = CatId, Kind = Kind, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    /// <summary>The cat refused, was away, or the dose was withheld: a recorded reason, not a missed task.</summary>
    public void Skip(Guid by, DateTimeOffset now, string reason)
    {
        if (Status == CareTaskStatus.Skipped) return;
        if (Status != CareTaskStatus.Pending)
            throw new DomainRuleViolationException(BusinessViolationCodes.CareTaskNotPending, "This task is no longer pending.", nameof(Status));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > IncidentRules.MaxSkipReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.CareTaskNotPending, "A reason is required.", nameof(reason));

        Status = CareTaskStatus.Skipped;
        CompletedOn = now;
        CompletedBy = by;
        SkipReason = reason.Trim();
        UpdateVersion();
    }

    /// <summary>Only a pending task can be voided (its schedule changed, or the stay ended); the key is freed so a regenerated task can take it.</summary>
    public void Void()
    {
        if (Status != CareTaskStatus.Pending) return;

        Status = CareTaskStatus.Voided;
        SourceKey = $"{SourceKey}#v{Id:N}";
        AssignedCaretakerId = null;
        UpdateVersion();
    }

    public void Assign(Guid? caretakerId)
    {
        if (Status != CareTaskStatus.Pending || AssignedCaretakerId == caretakerId) return;

        AssignedCaretakerId = caretakerId;
        UpdateVersion();
    }

    /// <summary>US-BRD-10 AC2: pending and past its due time plus the grace -- computed at read time by every task read and the dashboard.</summary>
    public bool IsOverdue(DateTimeOffset now, int graceMinutes) => IsOverdue(Status, DueOn, now, graceMinutes);

    /// <summary>The same rule over plain values, so a read can project the columns and still ask the question (the instance method delegates here).</summary>
    public static bool IsOverdue(CareTaskStatus status, DateTimeOffset dueOn, DateTimeOffset now, int graceMinutes) =>
        status == CareTaskStatus.Pending && now > dueOn.AddMinutes(graceMinutes);
}
