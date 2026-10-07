namespace CatEcommerce.Domain.Entities;

/// <summary>HO-ADM-001 §8.4. Owned by <c>StaffRosterAggregate</c>, once per pattern (current/next).
/// Invariants (Starts &lt; Ends, no midnight crossing, at most 3 per day, non-overlapping) are the
/// aggregate's.</summary>
public sealed class ShiftSlot
{
    public Guid Id { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly Starts { get; private set; }
    public TimeOnly Ends { get; private set; }
    public Guid? DefaultRoomId { get; private set; }

    private ShiftSlot() { }

    public ShiftSlot(Guid id, DayOfWeek dayOfWeek, TimeOnly starts, TimeOnly ends, Guid? defaultRoomId)
    {
        Id = id;
        DayOfWeek = dayOfWeek;
        Starts = starts;
        Ends = ends;
        DefaultRoomId = defaultRoomId;
    }
}
