namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BRD-001 section 8.18 / Phase H -- finders of the waitlist and daycare rows by a key the repository cannot filter on cheaply. Read-only ids and counts; the handler loads each aggregate it
/// mutates through its repository.
/// </summary>
public interface IBoardingWaitlistDaycareFinder
{
    /// <summary>The slot numbers already taken on a branch day by visits that are not cancelled.</summary>
    Task<IReadOnlySet<int>> GetTakenDaycareSlotsAsync(Guid branchId, DateOnly localDate, CancellationToken ct = default);

    /// <summary>The daycare rate card in force at an instant, or <c>null</c> (the branch does not offer daycare yet).</summary>
    Task<Guid?> FindDaycareRateCardInForceAsync(Guid branchId, DateTimeOffset instant, CancellationToken ct = default);

    /// <summary>Another live daycare visit of the cat on that day (one visit per cat per day), or <c>false</c>.</summary>
    Task<bool> CatHasDaycareVisitAsync(Guid catId, DateOnly localDate, CancellationToken ct = default);

    Task<int> CountLiveWaitlistAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Booked or checked-in daycare visits of the customer on or after <paramref name="fromDate"/>.</summary>
    Task<int> CountFutureDaycareVisitsAsync(Guid customerId, DateOnly fromDate, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> FindWaitlistByCustomerAsync(Guid customerId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> FindDaycareByCustomerAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Waiting entries, oldest first (the worker's notice pass).</summary>
    Task<IReadOnlyList<Guid>> FindWaitingEntriesAsync(CancellationToken ct = default);

    /// <summary>Waiting entries whose arrival date is before <paramref name="today"/> in their branch zone (the stale pass reads each entry's own branch date through the handler; this is the cheap superset).</summary>
    Task<IReadOnlyList<Guid>> FindWaitingEntriesArrivingBeforeAsync(DateOnly utcDateBound, CancellationToken ct = default);

    /// <summary>How many live <c>Notified</c> entries on the branch overlap the range -- owners already told about a free unit (no hold, but the notice pass does not announce one unit twice).</summary>
    Task<int> CountNotifiedOverlappingAsync(Guid branchId, DateOnly arrivalOn, DateOnly departureOn, Guid excludeEntryId, CancellationToken ct = default);

    /// <summary>Waiting entries older than <paramref name="entryId"/> on the branch whose range overlaps it -- they have the first claim on a free unit.</summary>
    Task<int> CountOlderWaitingOverlappingAsync(Guid branchId, DateOnly arrivalOn, DateOnly departureOn, DateTimeOffset joinedBefore, CancellationToken ct = default);

    /// <summary>Checked-in stays whose branch-local time has reached <paramref name="hour"/> -- the daily update candidates (ids, branch, local date).</summary>
    Task<IReadOnlyList<(Guid StayId, DateOnly LocalDate)>> FindDailyUpdateCandidatesAsync(DateTimeOffset now, Func<Guid, int> hourOfBranch, CancellationToken ct = default);
}
