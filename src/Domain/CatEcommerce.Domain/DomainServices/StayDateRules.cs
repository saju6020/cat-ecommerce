using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>A date rule that failed: the violation code and the plain-language message.</summary>
public sealed record StayDateViolation(string Code, string Message, string Property);

/// <summary>
/// HO-BRD-001 section 8.3/8.8 -- the date rules of a stay, ONE function shared by the aggregate (<c>CreateEnquiry</c>/<c>ChangeEnquiry</c>) and the pure
/// availability finder, so a range the finder refuses can never be created and vice versa. Pure.
/// </summary>
public static class StayDateRules
{
    /// <param name="isOnline">Online bookings are limited to the booking horizon and refused once the check-in window of arrival day has closed; staff are not.</param>
    public static StayDateViolation? Validate(
        DateOnly arrivalOn, DateOnly departureOn, DateTimeOffset now, string timeZoneId, TimeRange checkInWindow,
        int maxStayNights, int bookingHorizonDays, bool isOnline)
    {
        if (departureOn <= arrivalOn)
            return new StayDateViolation(BusinessViolationCodes.InvalidStayDates, "The departure must be after the arrival.", "DepartureOn");

        var today = BoardingOccupancyWindow.LocalDateOf(now, timeZoneId);
        if (arrivalOn < today)
            return new StayDateViolation(BusinessViolationCodes.InvalidStayDates, "The arrival cannot be in the past.", "ArrivalOn");

        var nights = departureOn.DayNumber - arrivalOn.DayNumber;
        if (nights > maxStayNights)
            return new StayDateViolation(BusinessViolationCodes.StayTooLong, $"A stay can be at most {maxStayNights} nights.", "DepartureOn");

        if (!isOnline) return null;

        if (arrivalOn > today.AddDays(bookingHorizonDays))
            return new StayDateViolation(BusinessViolationCodes.StayBeyondHorizon, $"Boarding can be booked up to {bookingHorizonDays} days ahead.", "ArrivalOn");

        if (arrivalOn == today)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var localNow = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
            if (localNow >= checkInWindow.End)
                return new StayDateViolation(BusinessViolationCodes.ArrivalTooLate, "Check-in for today has closed. Please choose a later arrival.", "ArrivalOn");
        }

        return null;
    }

    public static int Nights(DateOnly arrivalOn, DateOnly departureOn) => departureOn.DayNumber - arrivalOn.DayNumber;
}
