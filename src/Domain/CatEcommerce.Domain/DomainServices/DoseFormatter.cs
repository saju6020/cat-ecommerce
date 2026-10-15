using System.Globalization;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-PHM-001 §8.7 -- the wording of a dose on a medicine label: <c>Give 0.3 ml by mouth once daily for 5 days</c>. Built from the structured dose, route,
/// frequency and duration so the label never depends on free text, and every quantity carries its unit (UI-8). Pure.
/// </summary>
public static class DoseFormatter
{
    public static string Format(Dose dose, AdministrationRoute route, DoseFrequency frequency, int durationDays) =>
        $"Give {DoseText(dose)} {RouteLabel(route)} {frequency.Label} for {durationDays} {(durationDays == 1 ? "day" : "days")}";

    public static string DoseText(Dose dose) => $"{dose.AmountText} {DoseUnitLabel(dose.Unit, dose.Amount)}";

    public static string DoseUnitLabel(DoseUnit unit, decimal amount)
    {
        if (unit == DoseUnit.Mg) return "mg";
        if (unit == DoseUnit.Mcg) return "mcg";
        if (unit == DoseUnit.G) return "g";
        if (unit == DoseUnit.Ml) return "ml";
        if (unit == DoseUnit.Iu) return "IU";
        var singular = unit.DisplayName.ToLowerInvariant();
        return amount == 1m ? singular : singular + "s";
    }

    public static string RouteLabel(AdministrationRoute route)
    {
        if (route == AdministrationRoute.Oral) return "by mouth";
        if (route == AdministrationRoute.Subcutaneous) return "by injection under the skin";
        if (route == AdministrationRoute.Intramuscular) return "by injection into a muscle";
        if (route == AdministrationRoute.Intravenous) return "by injection into a vein";
        if (route == AdministrationRoute.Topical) return "on the skin";
        if (route == AdministrationRoute.Ophthalmic) return "into the eye";
        if (route == AdministrationRoute.Otic) return "into the ear";
        if (route == AdministrationRoute.Transdermal) return "as a skin patch";
        if (route == AdministrationRoute.Inhaled) return "by inhalation";
        return "rectally";
    }

    /// <summary><c>Quantity: 10 ml</c>, <c>Quantity: 14 tablets</c>.</summary>
    public static string QuantityLine(decimal quantity, DispensingUnit unit) =>
        $"Quantity: {quantity.ToString("0.##", CultureInfo.InvariantCulture)} {DispensingUnitLabel(unit, quantity)}";

    public static string DispensingUnitLabel(DispensingUnit unit, decimal quantity)
    {
        if (unit == DispensingUnit.Millilitre) return "ml";
        if (unit == DispensingUnit.Gram) return "g";
        var singular = unit.DisplayName.ToLowerInvariant();
        return quantity == 1m ? singular : singular + "s";
    }
}
