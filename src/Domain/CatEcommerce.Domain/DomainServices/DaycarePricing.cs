using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-BRD-001 section 8.18 -- the charge of a daycare visit, pure. <c>Day</c>: the day rate. <c>Hourly</c>: whole hours (rounded UP, at least one) x the hourly rate, never more than the day rate.</summary>
public static class DaycarePricing
{
    public static decimal Charge(DaycareBasis basis, decimal hourlyRate, decimal dayRate, TimeSpan stayed)
    {
        if (basis == DaycareBasis.Day) return dayRate;

        var hours = Math.Max(1m, Math.Ceiling((decimal)stayed.TotalHours));
        return Math.Min(dayRate, decimal.Round(hours * hourlyRate, 2, MidpointRounding.AwayFromZero));
    }
}
