using System.Globalization;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One cat of a stay as the generator needs it: its profile and the unit it sleeps in on the generated date.</summary>
public sealed record CareTaskCatInput(Guid CatId, string CatName, Guid UnitId, CareProfile Profile);

/// <summary>A task the generator proposes -- not yet a row. <see cref="SourceKey"/> makes generation idempotent per stay, cat, local date and key.</summary>
public sealed record CareTaskSeed(Guid CatId, Guid UnitId, CareTaskKind Kind, TimeOnly DueLocalTime, DateTimeOffset DueOn, string SourceKey, string Title);

/// <summary>
/// HO-BRD-001 section 8.9 (US-BRD-09, ADR-224, renumbered ADR-357) -- the daily task list of one stay: per cat, one task per feeding time, per medication time within its
/// <c>FromOn..ToOn</c>, and per routine time (litter, enrichment, observation check). Pure. The arrival day starts at the actual check-in instant; the departure day
/// ends at the check-out window's end. Keys are content-based (<c>feed:08:00</c>, <c>med:metacam:09:00</c>, <c>routine:LitterChange:09:00</c>), so a profile edit that
/// keeps a time keeps its task and a done task is never regenerated.
/// </summary>
public static class CareTaskGenerator
{
    public static IReadOnlyList<CareTaskSeed> Generate(
        DateOnly localDate, string timeZoneId, DateOnly arrivalOn, DateOnly departureOn, DateTimeOffset checkedInOn, TimeRange checkOutWindow,
        IReadOnlyList<CareTaskCatInput> cats, IReadOnlyList<RoutineTimeInput> routine)
    {
        if (localDate < arrivalOn || localDate > departureOn) return [];

        var checkOutEnd = BranchCalendar.ToInstant(departureOn, checkOutWindow.End, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).ToUniversalTime();
        var seeds = new List<CareTaskSeed>();

        void Add(CareTaskCatInput cat, CareTaskKind kind, TimeOnly time, string key, string title)
        {
            var due = BoardingOccupancyWindow.InstantOf(localDate, time, timeZoneId);
            if (due < checkedInOn || due > checkOutEnd) return;
            seeds.Add(new CareTaskSeed(cat.CatId, cat.UnitId, kind, time, due, key, Truncate(title)));
        }

        foreach (var cat in cats)
        {
            foreach (var feeding in cat.Profile.Feedings)
                Add(cat, CareTaskKind.Feed, feeding.Time, $"feed:{Hhmm(feeding.Time)}",
                    $"Feed {feeding.Portion} - {(feeding.FoodSource == FoodSource.HouseFood ? "house food" : "own food")}");

            foreach (var medication in cat.Profile.Medications)
            {
                if (medication.FromOn is { } from && localDate < from) continue;
                if (medication.ToOn is { } to && localDate > to) continue;

                foreach (var time in medication.Times)
                    Add(cat, CareTaskKind.Medication, time, $"med:{Slug(medication.Name)}:{Hhmm(time)}", $"Give {medication.Name} {medication.Dose} {medication.Route}");
            }

            foreach (var item in routine)
            {
                var kind = item.Kind == RoutineTaskKind.LitterChange ? CareTaskKind.LitterChange : item.Kind == RoutineTaskKind.Enrichment ? CareTaskKind.Enrichment : CareTaskKind.ObservationCheck;
                var title = kind == CareTaskKind.LitterChange ? "Change litter" : kind == CareTaskKind.Enrichment ? "Play and enrichment" : "Record observation";
                Add(cat, kind, item.Time, $"routine:{item.Kind.DisplayName}:{Hhmm(item.Time)}", title);
            }
        }

        return seeds.OrderBy(s => s.DueOn).ThenBy(s => s.CatId).ThenBy(s => s.SourceKey, StringComparer.Ordinal).ToList();
    }

    private static string Hhmm(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Slug(string name)
    {
        var chars = name.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).Take(40).ToArray();
        return new string(chars);
    }

    private static string Truncate(string title) => title.Length <= IncidentRules.MaxTaskTitleLength ? title : title[..IncidentRules.MaxTaskTitleLength];
}
