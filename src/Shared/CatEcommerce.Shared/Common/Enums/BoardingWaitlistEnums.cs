namespace CatEcommerce.Shared.Common.Enums;

// HO-BRD-001 §10.1 -- waitlist and daycare.

public sealed class BoardingWaitlistStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BoardingWaitlistStatus Waiting = new(1, nameof(Waiting));
    public static readonly BoardingWaitlistStatus Notified = new(2, nameof(Notified));
    public static readonly BoardingWaitlistStatus Withdrawn = new(3, nameof(Withdrawn));
    public static readonly BoardingWaitlistStatus Closed = new(4, nameof(Closed));
}

public sealed class DaycareBasis(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DaycareBasis Hourly = new(1, nameof(Hourly));
    public static readonly DaycareBasis Day = new(2, nameof(Day));
}

public sealed class DaycareVisitStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DaycareVisitStatus Booked = new(1, nameof(Booked));
    public static readonly DaycareVisitStatus CheckedIn = new(2, nameof(CheckedIn));
    public static readonly DaycareVisitStatus CheckedOut = new(3, nameof(CheckedOut));
    public static readonly DaycareVisitStatus Cancelled = new(4, nameof(Cancelled));
    public static readonly DaycareVisitStatus NoShow = new(5, nameof(NoShow));
}
