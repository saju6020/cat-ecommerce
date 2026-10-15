using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-SRG-001 §8.12 -- one follow-up rule of a protocol (owned by <c>SurgeryProtocolAggregate</c>).</summary>
public sealed class SurgeryProtocolFollowUp
{
    public Guid Id { get; private set; }
    public SurgeryFollowUpKind Kind { get; private set; } = default!;
    public int DayOffset { get; private set; }
    public int WindowFromDay { get; private set; }
    public int WindowToDay { get; private set; }

    private SurgeryProtocolFollowUp() { }

    public SurgeryProtocolFollowUp(Guid id, SurgeryFollowUpKind kind, int dayOffset, int windowFromDay, int windowToDay)
    {
        Id = id;
        Kind = kind;
        DayOffset = dayOffset;
        WindowFromDay = windowFromDay;
        WindowToDay = windowToDay;
    }
}

/// <summary>
/// HO-SRG-001 §8.6 (ADR-160) -- one of the five fixed pre-op items, owned by <c>SurgeryAggregate</c>. <see cref="ReferenceId"/> is the weight record or consent
/// record that completed it; <see cref="ReferenceDate"/> the weight record date (so a reschedule can decide whether the weight is still inside the window).
/// </summary>
public sealed class PreOpChecklistItem
{
    public Guid Id { get; private set; }
    public PreOpChecklistItemKind Kind { get; private set; } = default!;
    public ChecklistItemStatus Status { get; private set; } = default!;
    public Guid? CompletedBy { get; private set; }
    public DateTimeOffset? CompletedOn { get; private set; }
    public string? Note { get; private set; }
    public string? WaiverReason { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public DateOnly? ReferenceDate { get; private set; }

    private PreOpChecklistItem() { }

    public PreOpChecklistItem(Guid id, PreOpChecklistItemKind kind)
    {
        Id = id;
        Kind = kind;
        Status = ChecklistItemStatus.Outstanding;
    }

    public void Complete(Guid by, DateTimeOffset on, string? note, Guid? referenceId, DateOnly? referenceDate)
    {
        Status = ChecklistItemStatus.Done;
        CompletedBy = by;
        CompletedOn = on;
        Note = note;
        WaiverReason = null;
        ReferenceId = referenceId;
        ReferenceDate = referenceDate;
    }

    public void Waive(Guid by, DateTimeOffset on, string reason)
    {
        Status = ChecklistItemStatus.Waived;
        CompletedBy = by;
        CompletedOn = on;
        WaiverReason = reason;
        Note = null;
        ReferenceId = null;
        ReferenceDate = null;
    }

    /// <summary>Back to Outstanding -- the fact it recorded no longer holds (a reschedule, a withdrawn consent). Who and when are kept in the audit trail, not here.</summary>
    public void Revert()
    {
        Status = ChecklistItemStatus.Outstanding;
        CompletedBy = null;
        CompletedOn = null;
        Note = null;
        WaiverReason = null;
        ReferenceId = null;
        ReferenceDate = null;
    }
}

/// <summary>HO-SRG-001 §8.10 -- a post-op follow-up set at discharge. <see cref="WindowFrom"/>/<see cref="WindowTo"/> are the protocol window staff may move the due date within.</summary>
public sealed class SurgeryFollowUp
{
    public Guid Id { get; private set; }
    public SurgeryFollowUpKind Kind { get; private set; } = default!;
    public DateOnly DueOn { get; private set; }
    public DateOnly WindowFrom { get; private set; }
    public DateOnly WindowTo { get; private set; }
    public FollowUpStatus Status { get; private set; } = default!;
    public DateTimeOffset? ClosedOn { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public string? CancellationReason { get; private set; }

    private SurgeryFollowUp() { }

    public SurgeryFollowUp(Guid id, SurgeryFollowUpKind kind, DateOnly dueOn, DateOnly windowFrom, DateOnly windowTo)
    {
        Id = id;
        Kind = kind;
        DueOn = dueOn;
        WindowFrom = windowFrom;
        WindowTo = windowTo;
        Status = FollowUpStatus.Pending;
    }

    public void Close(FollowUpStatus status, DateTimeOffset on, Guid by, string? reason)
    {
        Status = status;
        ClosedOn = on;
        ClosedBy = by;
        CancellationReason = reason;
    }
}

/// <summary>HO-SRG-001 §8.3 -- one row per transition, never trimmed (C-ALL-4). <see cref="From"/> is null on the creation row.</summary>
public sealed class SurgeryStatusChange
{
    public Guid Id { get; private set; }
    public SurgeryStatus? From { get; private set; }
    public SurgeryStatus To { get; private set; } = default!;
    public DateTimeOffset ChangedOn { get; private set; }
    public Guid ChangedBy { get; private set; }
    public int Sequence { get; private set; }

    private SurgeryStatusChange() { }

    public SurgeryStatusChange(Guid id, SurgeryStatus? from, SurgeryStatus to, DateTimeOffset changedOn, Guid changedBy, int sequence)
    {
        Id = id;
        From = from;
        To = to;
        ChangedOn = changedOn;
        ChangedBy = changedBy;
        Sequence = sequence;
    }
}

/// <summary>HO-SRG-001 §8.3 (ADR-168) -- recovery-ward status history: who set which status and when. <see cref="Note"/> is plain text up to 500 characters.</summary>
public sealed class RecoveryStatusChange
{
    public Guid Id { get; private set; }
    public RecoveryStatus? From { get; private set; }
    public RecoveryStatus To { get; private set; } = default!;
    public DateTimeOffset ChangedOn { get; private set; }
    public Guid ChangedBy { get; private set; }
    public string? Note { get; private set; }
    public int Sequence { get; private set; }

    private RecoveryStatusChange() { }

    public RecoveryStatusChange(Guid id, RecoveryStatus? from, RecoveryStatus to, DateTimeOffset changedOn, Guid changedBy, string? note, int sequence)
    {
        Id = id;
        From = from;
        To = to;
        ChangedOn = changedOn;
        ChangedBy = changedBy;
        Note = note;
        Sequence = sequence;
    }
}

/// <summary>HO-SRG-001 section 8.2 -- an addendum to a FINALISED surgical report. Append-only (a trigger refuses any UPDATE or DELETE); owned by <c>SurgicalReportAggregate</c>.</summary>
public sealed class SurgicalReportAddendum
{
    public Guid Id { get; private set; }
    public string Text { get; private set; } = default!;
    public Guid AddedBy { get; private set; }
    public DateTimeOffset AddedOn { get; private set; }

    private SurgicalReportAddendum() { }

    public SurgicalReportAddendum(Guid id, string text, Guid addedBy, DateTimeOffset addedOn)
    {
        Id = id;
        Text = text;
        AddedBy = addedBy;
        AddedOn = addedOn;
    }
}

/// <summary>HO-SRG-001 section 8.11 -- one fixed price of a campaign, owned by <c>SurgeryCampaignAggregate</c>.</summary>
public sealed class CampaignServicePrice
{
    public Guid Id { get; private set; }
    public Guid ServiceId { get; private set; }
    public CatEcommerce.Domain.ValueObjects.Money Price { get; private set; } = default!;

    private CampaignServicePrice() { }

    public CampaignServicePrice(Guid id, Guid serviceId, CatEcommerce.Domain.ValueObjects.Money price)
    {
        Id = id;
        ServiceId = serviceId;
        Price = price;
    }
}
