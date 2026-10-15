namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Boarding's (HO-BRD-001 section 10.2) own registry keys. <c>boarding.depositPercent</c> already exists in
/// <c>SettingKeys.cs</c> (HO-ADM-001) and is consumed here, not redefined. <c>boarding.mediaRetentionMonths</c> of the handover is NOT
/// created: HO-XCT-001 already owns <c>retention.boardingMediaMonths</c> and the retention worker schedules the "boarding-media"
/// purge source with it (backlog XCT 2; HO-BRD-001 section 1 Amended).</summary>
public static partial class SettingKeys
{
    public const string BoardingDepositFixedAmount = "boarding.depositFixedAmount";
    public const string BoardingReservationHoldHours = "boarding.reservationHoldHours";
    public const string BoardingCheckInWindow = "boarding.checkInWindow";
    public const string BoardingCheckOutWindow = "boarding.checkOutWindow";
    public const string BoardingTurnaroundHours = "boarding.turnaroundHours";
    public const string BoardingLongStayThresholdNights = "boarding.longStayThresholdNights";
    public const string BoardingLongStayDiscountPercent = "boarding.longStayDiscountPercent";
    public const string BoardingBookingHorizonDays = "boarding.bookingHorizonDays";
    public const string BoardingMaxStayNights = "boarding.maxStayNights";
    public const string BoardingAlternativeSearchDays = "boarding.alternativeSearchDays";
    public const string BoardingCancellationWindowHours = "boarding.cancellationWindowHours";
    public const string BoardingEarlyCheckoutChargedNights = "boarding.earlyCheckoutChargedNights";
    public const string BoardingEarlyCheckoutRefundAsCredit = "boarding.earlyCheckoutRefundAsCredit";
    public const string BoardingNoShowGraceHours = "boarding.noShowGraceHours";
    public const string BoardingTaskOverdueGraceMinutes = "boarding.taskOverdueGraceMinutes";
    public const string BoardingIncidentAckEscalationMinutes = "boarding.incidentAckEscalationMinutes";
    public const string BoardingDailyUpdateHour = "boarding.dailyUpdateHour";
    public const string BoardingRequireBelongingsPhotos = "boarding.requireBelongingsPhotos";
    public const string BoardingDaycareDailyCapacity = "boarding.daycareDailyCapacity";
}
