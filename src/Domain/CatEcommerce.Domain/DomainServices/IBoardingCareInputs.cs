using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BRD-001 section 8.20 / F34 -- the caretaker shifts a task assignment needs, loaded once for a branch and a date range: staff holding the <c>Caretaker</c> role,
/// assigned to the branch, with a roster, their exceptions and approved leave, turned into instants by <see cref="StaffAvailability"/> WITHOUT restricting to opening hours
/// (boarding runs outside clinic hours). Read-only.
/// </summary>
public interface IBoardingCareInputs
{
    Task<IReadOnlyList<CaretakerShifts>> GetCaretakerShiftsAsync(Guid branchId, DateOnly from, DateOnly to, CancellationToken ct = default);
}

/// <summary>Finders of Boarding's own rows by a key the repository cannot filter on cheaply (read-only ids; the handler loads each aggregate it mutates).</summary>
public interface IBoardingTaskFinder
{
    /// <summary>Pending tasks of a stay (optionally one cat) due on or after <paramref name="fromLocalDate"/>.</summary>
    Task<IReadOnlyList<Guid>> FindPendingAsync(Guid stayId, Guid? catId, DateOnly fromLocalDate, CancellationToken ct = default);

    /// <summary>Pending tasks assigned to a caretaker with a local date inside the range (a leave being approved).</summary>
    Task<IReadOnlyList<Guid>> FindPendingAssignedToAsync(Guid caretakerId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Pending tasks of a stay that fall in an instant range (a cat away in surgery).</summary>
    Task<IReadOnlyList<Guid>> FindPendingInRangeAsync(Guid stayId, Guid catId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

public interface IBoardingStayFinder
{
    /// <summary>Stays that still need attention if the cat dies or changes owner: Reserved, Confirmed or CheckedIn stays carrying the cat.</summary>
    Task<IReadOnlyList<Guid>> FindLiveByCatAsync(Guid catId, CancellationToken ct = default);

    /// <summary>Every stay of a customer, for the merge reassignment (any status: history follows the survivor).</summary>
    Task<IReadOnlyList<Guid>> FindByCustomerAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>The CheckedIn stay carrying the cat on this branch, or <c>null</c> (surgery on a boarded cat, D-7).</summary>
    Task<Guid?> FindInHouseByCatAsync(Guid catId, CancellationToken ct = default);

    /// <summary>Live stays (Reserved, Confirmed, CheckedIn) holding <paramref name="unitId"/> with an active allocation that overlaps [from, to), other than <paramref name="excludeStayId"/> (an overstay).</summary>
    Task<IReadOnlyList<Guid>> FindOnUnitOverlappingAsync(Guid unitId, DateTimeOffset from, DateTimeOffset to, Guid excludeStayId, CancellationToken ct = default);

    /// <summary>The stay that owns a clinic referral (HO-EMR-001 answers a referral by its id), or <c>null</c>.</summary>
    Task<Guid?> FindByReferralAsync(Guid referralId, CancellationToken ct = default);

    /// <summary>CheckedIn stays of a branch (the worker's task-generation and daily-update passes).</summary>
    Task<IReadOnlyList<Guid>> FindCheckedInAsync(CancellationToken ct = default);
}
