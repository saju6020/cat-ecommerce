using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Infrastructure.Fakes.Administration;

/// <summary>
/// FAKE until HO-ADM-001 lands -- replace this registration.
///
/// Always returns an empty list of open intervals -- mirrors HO-ADM-001's own real default (§8.2:
/// "a new branch is closed until hours are set -- safe"), so it is a truthful fake rather than an
/// arbitrary denial.
/// </summary>
public sealed class InMemoryBranchCalendar : IBranchCalendar
{
    public Task<IReadOnlyList<TimeInterval>> OpenIntervalsAsync(Guid branchId, DateOnly from, DateOnly to, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TimeInterval>>(Array.Empty<TimeInterval>());
}
