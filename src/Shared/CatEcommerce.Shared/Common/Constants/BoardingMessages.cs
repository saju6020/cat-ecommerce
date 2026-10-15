namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-BRD-001 -- violation messages shared by more than one handler, aggregate or the constraint filter, so the sequential and
/// concurrent paths for one rule cannot drift (HO-ACC-001 section 20.6).</summary>
public static class BoardingMessages
{
    public const string UnitNoLongerAvailable = "That unit has just been booked. Please check availability again.";
    public const string CatAlreadyBoarding = "One of these cats already has a boarding stay that overlaps these dates.";
    public const string UnitCodeAlreadyExists = "A unit with this code already exists at this branch.";
    public const string DuplicateRateCardEffectiveDate = "A rate card already exists for this unit type with this effective date.";
    public const string MaintenanceOverlapsMaintenance = "This unit already has maintenance in that period.";
    public const string AlreadyWaitlisted = "You are already on the waitlist for these dates.";
    public const string DaycareFull = "Daycare is full for that day.";
    public const string StayNotFound = "Boarding stay not found.";
    public const string IsolationUnitRequired = "A stay with a suspected-infectious cat must be in an isolation unit.";
    public const string VaccinationGateBlocked = "A cat on this booking has a core vaccination that is due before departure.";

    /// <summary>"A cancelled stay cannot be checked in." -- the state, then what was attempted.</summary>
    public static string InvalidTransition(string from, string attempted) =>
        $"A {from.ToLowerInvariant()} stay cannot be {attempted}.";

    public static string CapacityExceeded(string unitTypeName, int capacity, int cats) =>
        $"A {unitTypeName} unit holds up to {capacity} cat{(capacity == 1 ? "" : "s")}. You have chosen {cats}.";
}
