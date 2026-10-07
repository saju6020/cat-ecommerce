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
        var items = new List<CatVaccinationStatusItemViewModel>();

        foreach (var vaccine in activeCoreVaccines)
        {
            var mostRecent = catVaccinations
                .Where(v => v.VaccineId == vaccine.VaccineId)
                .OrderByDescending(v => v.AdministeredOn)
                .FirstOrDefault();

            if (mostRecent is null)
            {
                items.Add(new CatVaccinationStatusItemViewModel(
                    vaccine.VaccineId, vaccine.Code, vaccine.Name,
                    VaccinationStatus.Overdue.DisplayName, null, "NeverAdministered"));
                continue;
            }

            var status =
                mostRecent.NextDueOn < asOf ? VaccinationStatus.Overdue
                : mostRecent.NextDueOn <= asOf.AddDays(dueSoonWindowDays) ? VaccinationStatus.DueSoon
                : VaccinationStatus.Current;

            items.Add(new CatVaccinationStatusItemViewModel(
                vaccine.VaccineId, vaccine.Code, vaccine.Name,
                status.DisplayName, mostRecent.NextDueOn, null));
        }

        var overallStatus = items.Count == 0
            ? VaccinationStatus.Current
            : Enumeration.FromDisplayName<VaccinationStatus>(
                items.Select(i => Enumeration.FromDisplayName<VaccinationStatus>(i.Status))
                     .OrderByDescending(s => s.Value)
                     .First()
                     .DisplayName);

        var knownDueDates = items.Where(i => i.NextDueOn is not null).Select(i => i.NextDueOn!.Value).ToList();
        DateOnly? earliestNextDueOn = knownDueDates.Count == 0 ? null : knownDueDates.Min();

        return new CatVaccinationStatusViewModel(
            catId,
            overallStatus.DisplayName,
            earliestNextDueOn,
            BookingEligible: overallStatus != VaccinationStatus.Overdue,
            items);
    }
}

public sealed record ActiveCoreVaccine(Guid VaccineId, string Code, string Name);

public sealed record CatVaccinationRecord(Guid VaccineId, DateOnly AdministeredOn, DateOnly NextDueOn);
