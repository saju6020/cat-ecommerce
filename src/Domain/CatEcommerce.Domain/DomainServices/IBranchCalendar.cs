using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Owned by HO-ADM-001 §8.9. Exact surface from §8.9's own table row. Loads the branch and its
/// closures, then calls the pure <c>BranchCalendar.OpenIntervals</c> (§8.5).
///
/// Until HO-ADM-001 lands, <c>Fakes/Administration/InMemoryBranchCalendar</c> always returns an
/// empty list -- no open intervals -- which mirrors HO-ADM-001's own real default (§8.2: "a new
/// branch is closed until hours are set -- safe"), so it is a truthful fake rather than an
/// arbitrary denial.
/// </summary>
public interface IBranchCalendar
{
    Task<IReadOnlyList<TimeInterval>> OpenIntervalsAsync(Guid branchId, DateOnly from, DateOnly to, CancellationToken ct);
}
