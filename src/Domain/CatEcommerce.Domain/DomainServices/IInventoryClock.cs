namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-IMP-001 §8.13 -- "the branch's today". Loads the branch's IANA zone and calls
/// <c>BranchCalendar.LocalDateOf</c> (ADR-045: zone conversion lives only in the calendar services). Every expiry decision in C#
/// goes through this plus <c>BatchExpiryRules</c>.
/// </summary>
public interface IInventoryClock
{
    Task<DateOnly> LocalTodayAsync(Guid branchId, CancellationToken ct);

    Task<string> TimeZoneIdAsync(Guid branchId, CancellationToken ct);

    /// <summary>The branch-local date of an arbitrary instant.</summary>
    Task<DateOnly> LocalDateOfAsync(Guid branchId, DateTimeOffset instant, CancellationToken ct);
}
