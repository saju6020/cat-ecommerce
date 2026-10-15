using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-BRD-001 section 10.2 -- the nineteen Boarding settings. A <see cref="SettingType.TimeRange"/> entry carries
/// <see cref="SettingBounds.StartBeforeEnd"/> as its <c>Max</c> marker: the registry validator then requires start &lt; end (U-A9); the
/// notification quiet hours (which wrap midnight) keep <c>null</c> bounds and are unaffected.</summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> BoardingEntries =
    [
        new SettingDefinition(SettingKeys.BoardingDepositFixedAmount, "Boarding", SettingType.Decimal, "0", "1000000", "amount", "0.00", true,
            "Boarding deposit (fixed amount)", "A fixed deposit for a reservation; 0 uses the deposit percentage instead (HO-BRD-001, US-BRD-06)."),
        new SettingDefinition(SettingKeys.BoardingReservationHoldHours, "Boarding", SettingType.Integer, "1", "168", "hours", "48", true,
            "Reservation hold", "How long a reserved unit is held awaiting the deposit before the reservation lapses (BR-16)."),
        new SettingDefinition(SettingKeys.BoardingCheckInWindow, "Boarding", SettingType.TimeRange, null, SettingBounds.StartBeforeEnd, null, "14:00-19:00", true,
            "Check-in window", "The local time window cats may be checked in; a unit is occupied from its start on the arrival date."),
        new SettingDefinition(SettingKeys.BoardingCheckOutWindow, "Boarding", SettingType.TimeRange, null, SettingBounds.StartBeforeEnd, null, "08:00-11:00", true,
            "Check-out window", "The local time window cats are collected; a unit is occupied until its end on the departure date, plus the turnaround."),
        new SettingDefinition(SettingKeys.BoardingTurnaroundHours, "Boarding", SettingType.Integer, "0", "48", "hours", "3", true,
            "Cleaning turnaround", "The gap kept between a departure and the next arrival in the same unit."),
        new SettingDefinition(SettingKeys.BoardingLongStayThresholdNights, "Boarding", SettingType.Integer, "2", "60", "nights", "14", true,
            "Long-stay threshold", "From this many nights the weekly (or monthly) rate card applies, with the long-stay discount."),
        new SettingDefinition(SettingKeys.BoardingLongStayDiscountPercent, "Boarding", SettingType.Decimal, "0", "50", "%", "0.00", true,
            "Long-stay discount", "The discount applied to each weekly or monthly rate unit of a long stay."),
        new SettingDefinition(SettingKeys.BoardingBookingHorizonDays, "Boarding", SettingType.Integer, "1", "365", "days", "180", true,
            "Boarding booking horizon", "How far ahead an owner can book online."),
        new SettingDefinition(SettingKeys.BoardingMaxStayNights, "Boarding", SettingType.Integer, "1", "365", "nights", "120", true,
            "Longest stay", "The most nights one reservation can cover."),
        new SettingDefinition(SettingKeys.BoardingAlternativeSearchDays, "Boarding", SettingType.Integer, "1", "60", "days", "14", true,
            "Alternative-dates search", "How many days either side are searched for alternative dates when the requested range is full."),
        new SettingDefinition(SettingKeys.BoardingCancellationWindowHours, "Boarding", SettingType.Integer, "0", "720", "hours", "72", true,
            "Cancellation window", "A cancellation closer to arrival than this forfeits the deposit (BR-12)."),
        new SettingDefinition(SettingKeys.BoardingEarlyCheckoutChargedNights, "Boarding", SettingType.Integer, "0", "7", "nights", "0", true,
            "Early check-out extra nights", "Nights charged beyond those used when a cat leaves early."),
        new SettingDefinition(SettingKeys.BoardingEarlyCheckoutRefundAsCredit, "Boarding", SettingType.Boolean, null, null, null, "false", true,
            "Early check-out refund as credit", "When true the early check-out difference is credited to the customer instead of refunded."),
        new SettingDefinition(SettingKeys.BoardingNoShowGraceHours, "Boarding", SettingType.Integer, "0", "72", "hours", "12", true,
            "Boarding no-show grace", "Hours after the arrival window opens before a no-show may be recorded."),
        new SettingDefinition(SettingKeys.BoardingTaskOverdueGraceMinutes, "Boarding", SettingType.Integer, "0", "240", "minutes", "30", true,
            "Care task overdue grace", "Minutes after the due time before a pending care task is shown as overdue."),
        new SettingDefinition(SettingKeys.BoardingIncidentAckEscalationMinutes, "Boarding", SettingType.Integer, "5", "240", "minutes", "30", true,
            "Incident escalation", "Minutes an unacknowledged high-severity incident waits before a phone-call follow-up is raised."),
        new SettingDefinition(SettingKeys.BoardingDailyUpdateHour, "Boarding", SettingType.Integer, "0", "23", "hour", "18", true,
            "Daily update hour", "The branch-local hour the owner's daily boarding update is sent."),
        new SettingDefinition(SettingKeys.BoardingRequireBelongingsPhotos, "Boarding", SettingType.Boolean, null, null, null, "true", true,
            "Require belongings photos", "When true, belongings listed at check-in need at least one photo."),
        new SettingDefinition(SettingKeys.BoardingDaycareDailyCapacity, "Boarding", SettingType.Integer, "0", "100", "visits", "0", true,
            "Daycare daily capacity", "Daycare visits per branch per day; 0 means daycare is not offered."),
    ];
}

/// <summary>Marker values a <see cref="SettingDefinition"/> may carry in <c>Min</c>/<c>Max</c> for a type whose bounds are not numeric.</summary>
public static class SettingBounds
{
    /// <summary>A <see cref="SettingType.TimeRange"/> whose start must be earlier than its end (it may not wrap midnight).</summary>
    public const string StartBeforeEnd = "start<end";
}
