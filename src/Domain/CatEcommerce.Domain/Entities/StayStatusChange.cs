using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-BRD-001 section 8.3 -- one row per stay transition, never trimmed (C-ALL-4); ordered by <see cref="Sequence"/>. Owned by the stay.</summary>
public sealed class StayStatusChange
{
    public Guid Id { get; private set; }
    public BoardingStayStatus From { get; private set; } = default!;
    public BoardingStayStatus To { get; private set; } = default!;
    public DateTimeOffset ChangedOn { get; private set; }
    public Guid ChangedBy { get; private set; }
    public int Sequence { get; private set; }

    private StayStatusChange() { }

    public StayStatusChange(Guid id, BoardingStayStatus from, BoardingStayStatus to, DateTimeOffset changedOn, Guid changedBy, int sequence)
    {
        Id = id;
        From = from;
        To = to;
        ChangedOn = changedOn;
        ChangedBy = changedBy;
        Sequence = sequence;
    }
}
