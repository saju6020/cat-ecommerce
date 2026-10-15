using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>A branch's boarding settings in force, resolved once per request through <c>IBusinessSettings</c> (branch overrides applied).</summary>
public sealed record BoardingBranchSettings(
    string TimeZoneId, TimeRange CheckInWindow, TimeRange CheckOutWindow, int TurnaroundHours, int MaxStayNights, int BookingHorizonDays,
    int AlternativeSearchDays, int ReservationHoldHours, decimal DepositPercent, decimal DepositFixedAmount, PricingSnapshot Pricing, string CurrencyCode,
    int VaccinationDueSoonDays, int CancellationWindowHours, int NoShowGraceHours, bool RequireBelongingsPhotos, int TaskOverdueGraceMinutes);

/// <summary>The pre-loaded inputs of <see cref="BoardingAvailabilityFinder"/> for one branch and date range.</summary>
public sealed record BoardingInputSet(
    BoardingBranchSettings Settings,
    IReadOnlyList<AvailabilityUnit> Units,
    IReadOnlyDictionary<Guid, IReadOnlyList<OccupancyRange>> BusyUnits,
    IReadOnlyDictionary<int, RateCardSnapshot> RateCards);

/// <summary>Another live stay of a cat that overlaps a requested range -- its DATES, never its id (CatAlreadyBoarding names dates only).</summary>
public sealed record CatOverlap(Guid CatId, DateOnly ArrivalOn, DateOnly DepartureOn);

/// <summary>
/// HO-BRD-001 section 8.20/11.4 -- ONE place that loads what availability and every unit-taking command need for a branch and a date range: the branch's
/// units, the busy ranges (active allocations plus live maintenance blocks, excluding the stay being changed), the rate cards in force, and the settings.
/// Implemented read-only in <c>Infrastructure/Boarding</c>.
/// </summary>
public interface IBoardingInputs
{
    /// <summary>The branch's settings, or <c>null</c> when the branch does not exist.</summary>
    Task<BoardingBranchSettings?> GetSettingsAsync(Guid branchId, CancellationToken ct = default);

    /// <summary>Units, busy ranges and rate cards for <paramref name="arrivalOn"/> .. <paramref name="departureOn"/>, widened by the alternative-search radius.</summary>
    Task<BoardingInputSet?> LoadAsync(Guid branchId, DateOnly arrivalOn, DateOnly departureOn, Guid? excludeStayId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Live stays (active stay-cat rows) of any of <paramref name="catIds"/> that overlap <paramref name="range"/> (BR-13, D-7).</summary>
    Task<IReadOnlyList<CatOverlap>> FindCatOverlapsAsync(IReadOnlyCollection<Guid> catIds, OccupancyRange range, Guid? excludeStayId, CancellationToken ct = default);
}
