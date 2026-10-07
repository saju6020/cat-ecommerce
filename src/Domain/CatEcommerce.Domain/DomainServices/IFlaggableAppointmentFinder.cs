namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-APT-001 §8.10/§11.6 -- finds future <c>Booked</c> appointments a flagging domain event handler
/// must loop over. Not named in §8.12's own port table -- added for the same reason as
/// <see cref="IStaleHoldReleaser"/>: <c>IAggregateRootRepository&lt;T&gt;</c>'s generic contract has
/// no "find every matching row" method.
/// </summary>
public interface IFlaggableAppointmentFinder
{
    /// <summary>Booked appointments performed by <paramref name="performerId"/> whose local start
    /// date (in their own branch's zone) falls in <c>[from, to]</c> inclusive.</summary>
    Task<IReadOnlyList<Guid>> FindByPerformerInRangeAsync(Guid performerId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Booked appointments at <paramref name="branchId"/> whose local start date falls in
    /// <c>[from, to]</c> inclusive and, when <paramref name="partialFrom"/>/<paramref name="partialTo"/>
    /// are given, whose local start time also falls inside that daily window (a partial closure).</summary>
    Task<IReadOnlyList<Guid>> FindByBranchInRangeAsync(Guid branchId, DateOnly from, DateOnly to, TimeOnly? partialFrom, TimeOnly? partialTo, CancellationToken ct = default);

    /// <summary>Every future (StartsOn &gt; now) Booked appointment for the cat.</summary>
    Task<IReadOnlyList<Guid>> FindFutureByCatAsync(Guid catId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Every appointment currently flagged with this exact source id (for clearing).</summary>
    Task<IReadOnlyList<Guid>> FindFlaggedBySourceAsync(Guid sourceId, CancellationToken ct = default);

    /// <summary>Every appointment owned by <paramref name="customerId"/> -- HO-ACC-001
    /// <c>CustomerMergedEvent</c>'s reassignment contract (§11.6).</summary>
    Task<IReadOnlyList<Guid>> FindByCustomerAsync(Guid customerId, CancellationToken ct = default);
}
