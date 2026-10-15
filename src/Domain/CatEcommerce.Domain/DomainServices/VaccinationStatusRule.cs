using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>An active core vaccine the rule checks a cat against (HO-CAT-001 Q4).</summary>
public sealed record VaccinationRuleVaccine(Guid VaccineId, string Code, string Name);

/// <summary>One recorded vaccination, reduced to what the rule reads.</summary>
public sealed record VaccinationRuleRecord(Guid VaccineId, DateOnly AdministeredOn, DateOnly NextDueOn);

/// <summary>The outcome for one core vaccine. <see cref="Reason"/> is <c>NeverAdministered</c> or <c>null</c>.</summary>
public sealed record VaccinationRuleItem(Guid VaccineId, string Code, string Name, VaccinationStatus Status, DateOnly? NextDueOn, string? Reason);

public sealed record VaccinationEvaluation(VaccinationStatus OverallStatus, IReadOnlyList<VaccinationRuleItem> Items, DateOnly? EarliestNextDueOn);

/// <summary>
/// HO-BRD-001 section 8.6 (ADR-222, renumbered ADR-355): HO-CAT-001 Q4's algorithm, moved here verbatim so that the cat's vaccination status read
/// (<c>asOf</c> = today) and the boarding gate (<c>asOf</c> = the departure date) are the same rule, never a copy of it. Pure.
///
/// A core vaccine never administered is Overdue (<c>NeverAdministered</c>); otherwise the most recent dose decides: due before <c>asOf</c> is
/// Overdue, due within <c>dueSoonDays</c> is DueSoon, anything later is Current. The overall status is the worst item; no core vaccine configured is Current.
/// </summary>
public static class VaccinationStatusRule
{
    public const string NeverAdministered = nameof(NeverAdministered);

    public static VaccinationEvaluation Evaluate(
        IReadOnlyList<VaccinationRuleVaccine> activeCoreVaccines,
        IReadOnlyList<VaccinationRuleRecord> catVaccinations,
        DateOnly asOf,
        int dueSoonDays)
    {
        var items = new List<VaccinationRuleItem>();

        foreach (var vaccine in activeCoreVaccines)
        {
            var mostRecent = catVaccinations
                .Where(v => v.VaccineId == vaccine.VaccineId)
                .OrderByDescending(v => v.AdministeredOn)
                .FirstOrDefault();

            if (mostRecent is null)
            {
                items.Add(new VaccinationRuleItem(vaccine.VaccineId, vaccine.Code, vaccine.Name, VaccinationStatus.Overdue, null, NeverAdministered));
                continue;
            }

            var status =
                mostRecent.NextDueOn < asOf ? VaccinationStatus.Overdue
                : mostRecent.NextDueOn <= asOf.AddDays(dueSoonDays) ? VaccinationStatus.DueSoon
                : VaccinationStatus.Current;

            items.Add(new VaccinationRuleItem(vaccine.VaccineId, vaccine.Code, vaccine.Name, status, mostRecent.NextDueOn, null));
        }

        var overall = items.Count == 0
            ? VaccinationStatus.Current
            : items.Select(i => i.Status).OrderByDescending(s => s.Value).First();

        var knownDueDates = items.Where(i => i.NextDueOn is not null).Select(i => i.NextDueOn!.Value).ToList();
        DateOnly? earliest = knownDueDates.Count == 0 ? null : knownDueDates.Min();

        return new VaccinationEvaluation(overall, items, earliest);
    }
}
