using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Read.QueryHandlers.Cat;

/// <summary>
/// The US-CAT-07 algorithm (HO-CAT-001 section 12.3, Q4), factored into a pure function so it
/// can be unit-tested without a database. GetCatVaccinationStatusQueryHandler is the only
/// caller; it loads the two inputs from CatEcommerceDbContext and passes them here.
/// </summary>
public static class CatVaccinationStatusCalculator
{
    public static CatVaccinationStatusViewModel Calculate(
        Guid catId,
        IReadOnlyList<ActiveCoreVaccine> activeCoreVaccines,
        IReadOnlyList<CatVaccinationRecord> catVaccinations,
        DateOnly asOf,
        int dueSoonWindowDays)
    {
        // HO-BRD-001 ADR-355: the algorithm itself is the Domain's VaccinationStatusRule, shared with the boarding gate; this method only maps its output.
        var evaluation = VaccinationStatusRule.Evaluate(
            activeCoreVaccines.Select(v => new VaccinationRuleVaccine(v.VaccineId, v.Code, v.Name)).ToList(),
            catVaccinations.Select(v => new VaccinationRuleRecord(v.VaccineId, v.AdministeredOn, v.NextDueOn)).ToList(),
            asOf, dueSoonWindowDays);

        var items = evaluation.Items
            .Select(i => new CatVaccinationStatusItemViewModel(i.VaccineId, i.Code, i.Name, i.Status.DisplayName, i.NextDueOn, i.Reason))
            .ToList();

        return new CatVaccinationStatusViewModel(
            catId,
            evaluation.OverallStatus.DisplayName,
            evaluation.EarliestNextDueOn,
            BookingEligible: evaluation.OverallStatus != VaccinationStatus.Overdue,
            items);
    }
}

public sealed record ActiveCoreVaccine(Guid VaccineId, string Code, string Name);

public sealed record CatVaccinationRecord(Guid VaccineId, DateOnly AdministeredOn, DateOnly NextDueOn);
