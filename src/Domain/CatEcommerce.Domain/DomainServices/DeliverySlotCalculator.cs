using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.8, US-ORD-03 AC1 -- pure. The delivery slots a service level offers: its windows on the next seven dates that have windows, on
/// or after <c>today + LeadTimeDays</c> (branch-local), skipping dates the branch is closed (HO-ADM-001 <c>IBranchCalendar</c>; the caller says
/// which dates are open). <b>Slots are preference windows with no capacity</b> (OQ-13). A service level without windows offers no slot choice
/// ("where applicable"). The scan looks at most four weeks ahead, so a long closure cannot spin.
/// </summary>
public static class DeliverySlotCalculator
{
    private const int ScanDays = 28;

    public static IReadOnlyList<DeliverySlot> Slots(IReadOnlyList<SlotWindow> windows, int leadTimeDays, DateOnly todayLocal, Func<DateOnly, bool> isOpen)
    {
        if (windows.Count == 0) return [];

        var first = todayLocal.AddDays(leadTimeDays);
        var slots = new List<DeliverySlot>();
        var datesListed = 0;

        for (var offset = 0; offset < ScanDays && datesListed < DeliveryZoneRules.SlotLookaheadDays; offset++)
        {
            var date = first.AddDays(offset);
            var onDate = windows.Where(w => w.DayOfWeek == date.DayOfWeek).OrderBy(w => w.Start).ToList();
            if (onDate.Count == 0 || !isOpen(date)) continue;

            slots.AddRange(onDate.Select(w => new DeliverySlot(date, w.Start, w.End, w.Label)));
            datesListed++;
        }

        return slots;
    }

    /// <summary>Whether the chosen date and window start are one the service offers (the placement check): the window exists on that weekday,
    /// the date is not before <c>today + lead time</c>, and it is among the dates <see cref="Slots"/> lists.</summary>
    public static bool IsOffered(IReadOnlyList<SlotWindow> windows, int leadTimeDays, DateOnly todayLocal, Func<DateOnly, bool> isOpen, DateOnly date, TimeOnly windowStart) =>
        Slots(windows, leadTimeDays, todayLocal, isOpen).Any(s => s.Date == date && s.WindowStart == windowStart);
}
