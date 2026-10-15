using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>What checking a stay out NOW would change: whether it is early, the nights charged, the re-quote, the departure those nights end on and the range the unit is held until.</summary>
public sealed record EarlyCheckoutOutcome(bool IsEarly, int BookedNights, int ChargedNights, BoardingQuote? Quote, DateOnly ChargedDepartureOn, OccupancyRange Window, bool IsLate = false);

/// <summary>
/// HO-BRD-001 section 8.15 (US-BRD-16) -- the ONE early check-out calculation. The check-out handler and the owner-authorised PREVIEW (2026-10-14, "Show before") both call it, so what staff are shown
/// is exactly what the check-out then does. Early, on time, or late (an overstay is charged). Pure.
/// </summary>
public static class EarlyCheckout
{
    public static EarlyCheckoutOutcome Compute(BoardingStayAggregate stay, DateOnly today, DateTimeOffset now, int turnaroundHours) =>
        Compute(
            new EarlyCheckoutInput(stay.Status, stay.ArrivalOn, stay.DepartureOn, stay.Cats.Count, stay.OccupiedFrom, stay.OccupiedTo, stay.RateCardSnapshot, stay.PricingSnapshot),
            today, now, turnaroundHours);

    /// <summary>The same calculation over plain values: the preview READS these columns (a query handler never rehydrates the stay) and the aggregate overload delegates here, so they cannot drift.</summary>
    public static EarlyCheckoutOutcome Compute(EarlyCheckoutInput stay, DateOnly today, DateTimeOffset now, int turnaroundHours)
    {
        var window = new OccupancyRange(stay.OccupiedFrom, stay.OccupiedTo);
        var booked = StayDateRules.Nights(stay.ArrivalOn, stay.DepartureOn);
        if (stay.Status == BoardingStayStatus.CheckedIn && today < stay.DepartureOn && stay.Card is { } card && stay.Pricing is { } pricing)
        {
            var charged = BoardingCheckoutRules.NightsToCharge(stay.ArrivalOn, stay.DepartureOn, today, pricing.EarlyCheckoutChargedNights);
            if (charged < booked)
                return new EarlyCheckoutOutcome(true, booked, charged, BoardingQuoteCalculator.Quote(charged, stay.CatCount, card, pricing), stay.ArrivalOn.AddDays(charged),
                    BoardingOccupancyWindow.TruncateAtCheckOut(window, now, turnaroundHours));
        }

        // A check-out AFTER the booked departure is an overstay (CODE_REVIEW 2026-10-15): the extra nights are charged (re-quoted with the stay snapshots) and the allocation is extended to the
        // check-out plus the turnaround, so the unit cannot be re-booked while the cat is still in it. A collision with the next booking is the exclusion constraint's 409.
        if (stay.Status == BoardingStayStatus.CheckedIn && today > stay.DepartureOn && stay.Card is { } lateCard && stay.Pricing is { } latePricing)
        {
            // Owner 2026-10-15: "Charge extra nights, never block". The extra nights are CHARGES ONLY: the allocation is NOT extended (an extension could hit ex_stay_allocation_unit and
            // refuse the check-out of a cat that is leaving). A booking that overlaps the overstay is flagged for staff by an event handler instead.
            var charged = today.DayNumber - stay.ArrivalOn.DayNumber;
            return new EarlyCheckoutOutcome(false, booked, charged, BoardingQuoteCalculator.Quote(charged, stay.CatCount, lateCard, latePricing), stay.ArrivalOn.AddDays(charged), window, IsLate: true);
        }

        return new EarlyCheckoutOutcome(false, booked, booked, null, stay.DepartureOn, window);
    }
}

/// <summary>How a credit owed by an early check-out is settled: nothing, account credit, or a refund (the snapshotted <c>boarding.earlyCheckoutRefundAsCredit</c>).</summary>
public static class EarlyCheckoutCredit
{
    public const string None = nameof(None);
    public const string Credit = nameof(Credit);
    public const string Refund = nameof(Refund);

    public static string Outcome(decimal creditDue, bool? refundAsCredit) => creditDue <= 0m ? None : refundAsCredit == true ? Credit : Refund;
}

/// <summary>The columns the early check-out calculation reads.</summary>
public sealed record EarlyCheckoutInput(
    BoardingStayStatus Status, DateOnly ArrivalOn, DateOnly DepartureOn, int CatCount, DateTimeOffset OccupiedFrom, DateTimeOffset OccupiedTo, RateCardSnapshot? Card, PricingSnapshot? Pricing);
