using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-IMP-001 §8.12 -- the owned recall record of a <c>BatchAggregate</c> (owned ONE). Replaced when a withdrawn
/// batch is recalled again; the status history and the audit capture keep the earlier one.</summary>
public sealed class BatchRecall
{
    public string RecallNumber { get; private set; } = default!;
    public string Reason { get; private set; } = default!;
    public string CustomerAdvice { get; private set; } = default!;
    public DateTimeOffset DeclaredOn { get; private set; }
    public Guid DeclaredBy { get; private set; }
    public DateTimeOffset? NotifiedOn { get; private set; }
    public Guid? NotifiedBy { get; private set; }
    public int NotifiedRecipientCount { get; private set; }
    public int NotificationRuns { get; private set; }
    public DateTimeOffset? ClosedOn { get; private set; }
    public string? ClosingNote { get; private set; }

    private BatchRecall() { }

    public BatchRecall(string recallNumber, string reason, string customerAdvice, DateTimeOffset declaredOn, Guid declaredBy)
    {
        RecallNumber = recallNumber;
        Reason = reason;
        CustomerAdvice = customerAdvice;
        DeclaredOn = declaredOn;
        DeclaredBy = declaredBy;
    }

    internal void RecordNotification(int count, DateTimeOffset on, Guid by)
    {
        NotifiedOn = on;
        NotifiedBy = by;
        NotifiedRecipientCount += count;
        NotificationRuns++;
    }

    internal void Close(DateTimeOffset on, string note)
    {
        ClosedOn = on;
        ClosingNote = note;
    }
}

/// <summary>One row per batch status transition, never trimmed (§8.6.1).</summary>
public sealed class BatchStatusChange
{
    public Guid Id { get; private set; }
    public BatchStatus From { get; private set; } = default!;
    public BatchStatus To { get; private set; } = default!;
    public string? Reason { get; private set; }
    public DateTimeOffset ChangedOn { get; private set; }
    public Guid ChangedBy { get; private set; }
    public int Sequence { get; private set; }

    private BatchStatusChange() { }

    public BatchStatusChange(Guid id, BatchStatus from, BatchStatus to, string? reason, DateTimeOffset changedOn, Guid changedBy, int sequence)
    {
        Id = id;
        From = from;
        To = to;
        Reason = reason;
        ChangedOn = changedOn;
        ChangedBy = changedBy;
        Sequence = sequence;
    }
}
