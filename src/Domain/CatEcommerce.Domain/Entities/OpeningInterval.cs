namespace CatEcommerce.Domain.Entities;

/// <summary>HO-ADM-001 §8.2. Owned by <c>BranchAggregate</c>. <c>DayOfWeek</c> is the .NET enum
/// (Sunday=0..Saturday=6); invariants (Opens &lt; Closes, no midnight crossing, at most 3 per day,
/// non-overlapping, sorted) are enforced by the aggregate, not here.</summary>
public sealed class OpeningInterval
{
    public Guid Id { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly Opens { get; private set; }
    public TimeOnly Closes { get; private set; }

    private OpeningInterval() { }

    public OpeningInterval(Guid id, DayOfWeek dayOfWeek, TimeOnly opens, TimeOnly closes)
    {
        Id = id;
        DayOfWeek = dayOfWeek;
        Opens = opens;
        Closes = closes;
    }
}
