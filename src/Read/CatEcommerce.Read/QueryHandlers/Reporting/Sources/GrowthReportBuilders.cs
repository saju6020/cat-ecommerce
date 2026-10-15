using System.Globalization;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Sources;

/// <summary>One money event of a customer (the survivor of a merge): an invoice (positive) or a credit note (negative) on a business line.</summary>
public sealed record ActivityEvent(Guid CustomerId, DateTimeOffset At, int BusinessLine, decimal Net, bool IsCredit);

/// <summary>
/// HO-RPT-001 section 8.3 R18 (ADR-291), <b>pure</b>. An <b>active</b> customer in a period has at least one issued invoice in it; a customer is <b>new</b> when the first invoice they ever had falls in the
/// period and <b>returning</b> when they had one before; the acquisition source is the registration channel; a merged customer is counted once because the events are already the survivor's.
/// </summary>
public static class NewVersusReturningBuilder
{
    public static (IReadOnlyList<ReportRow> Rows, ReportRow Totals) Build(
        IEnumerable<ActivityEvent> invoicesInRange, IReadOnlyDictionary<Guid, DateTimeOffset> firstEver, IReadOnlyDictionary<Guid, int> channelOf,
        IEnumerable<(DateTimeOffset At, int Channel)> registrations, DateOnly from, DateOnly to, TimeBucket bucket, Func<DateTimeOffset, DateOnly> localDate)
    {
        string Label(DateTimeOffset at) => ReportBuckets.Label(localDate(at), bucket);

        var active = invoicesInRange.Where(e => !e.IsCredit).GroupBy(e => Label(e.At)).ToDictionary(g => g.Key, g => g.Select(e => e.CustomerId).Distinct().ToList());
        var registered = registrations.GroupBy(r => Label(r.At)).ToDictionary(g => g.Key, g => g.ToList());

        var labels = new List<string>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var label = ReportBuckets.Label(day, bucket);
            if (!labels.Contains(label)) labels.Add(label);
        }

        var rows = new List<ReportRow>();
        foreach (var label in labels)
        {
            var customers = active.GetValueOrDefault(label) ?? [];
            var created = customers.Where(c => firstEver.TryGetValue(c, out var first) && Label(first) == label).ToList();
            var regs = registered.GetValueOrDefault(label) ?? [];

            rows.Add(new ReportRow
            {
                ["bucket"] = label,
                ["activeCustomers"] = (long)customers.Count,
                ["newCustomers"] = (long)created.Count,
                ["returningCustomers"] = (long)(customers.Count - created.Count),
                ["newSelfService"] = (long)created.Count(c => channelOf.GetValueOrDefault(c) == RegistrationChannel.SelfService.Value),
                ["newReception"] = (long)created.Count(c => channelOf.GetValueOrDefault(c) == RegistrationChannel.Reception.Value),
                ["registrations"] = (long)regs.Count,
            });
        }

        var everyone = invoicesInRange.Where(e => !e.IsCredit).Select(e => e.CustomerId).Distinct().ToList();
        var allNew = everyone.Where(c => firstEver.TryGetValue(c, out var first) && localDate(first) >= from && localDate(first) <= to).ToList();
        var totals = new ReportRow
        {
            ["bucket"] = "Total",
            ["activeCustomers"] = (long)everyone.Count,
            ["newCustomers"] = (long)allNew.Count,
            ["returningCustomers"] = (long)(everyone.Count - allNew.Count),
            ["newSelfService"] = (long)allNew.Count(c => channelOf.GetValueOrDefault(c) == RegistrationChannel.SelfService.Value),
            ["newReception"] = (long)allNew.Count(c => channelOf.GetValueOrDefault(c) == RegistrationChannel.Reception.Value),
            ["registrations"] = (long)registered.Values.Sum(v => v.Count),
        };

        return (rows, totals);
    }
}

/// <summary>
/// HO-RPT-001 section 8.3 R19, <b>pure</b>. Lifetime value = net revenue (invoices less credit notes) across all lines to the end of the range, for customers first seen inside it. Shown in value bands and as the
/// fifty highest customers; the shares are each line's part of the group's revenue.
/// </summary>
public static class LifetimeValueBuilder
{
    public const int TopCustomers = 50;

    private static readonly (decimal Min, string Label)[] Bands =
    [
        (0m, "Under 1,000"), (1_000m, "1,000 to 4,999"), (5_000m, "5,000 to 19,999"), (20_000m, "20,000 and over"),
    ];

    public static string BandOf(decimal ltv) => Bands.Last(b => ltv >= b.Min || b.Min == 0m).Label;

    public static IReadOnlyList<ReportRow> Build(IEnumerable<ActivityEvent> events, IReadOnlyDictionary<Guid, string> names, string currency)
    {
        var customers = events.GroupBy(e => e.CustomerId).Select(g => new
        {
            Id = g.Key,
            Ltv = g.Sum(e => e.Net),
            Clinic = g.Where(e => e.BusinessLine == BusinessLine.Clinic.Value).Sum(e => e.Net),
            Shop = g.Where(e => e.BusinessLine == BusinessLine.Shop.Value).Sum(e => e.Net),
            Boarding = g.Where(e => e.BusinessLine == BusinessLine.Boarding.Value).Sum(e => e.Net),
            Lines = g.Where(e => !e.IsCredit && e.Net > 0m).Select(e => BusinessLineRules.Classify(e.BusinessLine)).Distinct().Order().ToList(),
        }).ToList();

        ReportRow Row(string section, string label, int count, decimal total, decimal clinic, decimal shop, decimal boarding, string lines) => new()
        {
            ["section"] = section,
            ["label"] = label,
            ["customers"] = (long)count,
            ["averageLtv"] = count == 0 ? null : new ReportMoney(ReportMath.Money(total / count), currency),
            ["clinicShare"] = total > 0m ? ReportMath.Percent(clinic, total) : null,
            ["shopShare"] = total > 0m ? ReportMath.Percent(shop, total) : null,
            ["boardingShare"] = total > 0m ? ReportMath.Percent(boarding, total) : null,
            ["linesUsed"] = lines,
        };

        var rows = new List<ReportRow>();
        foreach (var band in Bands)
        {
            var inBand = customers.Where(c => BandOf(c.Ltv) == band.Label).ToList();
            if (inBand.Count == 0) continue;
            rows.Add(Row("Band", band.Label, inBand.Count, inBand.Sum(c => c.Ltv), inBand.Sum(c => c.Clinic), inBand.Sum(c => c.Shop), inBand.Sum(c => c.Boarding), ""));
        }

        foreach (var c in customers.OrderByDescending(c => c.Ltv).Take(TopCustomers))
            rows.Add(Row("Top 50", names.GetValueOrDefault(c.Id) ?? "Customer", 1, c.Ltv, c.Clinic, c.Shop, c.Boarding, string.Join(", ", c.Lines)));

        return rows;
    }
}

/// <summary>
/// HO-RPT-001 section 8.3 R20 (OQ-8), <b>pure</b>. Monthly cohorts by the month of the customer's first invoice; the percentage of the cohort with a qualifying invoice in cohort month + 1 ... 12. The measure is
/// any line, a repeat purchase (a shop invoice line) or a repeat visit (a clinic invoice line). A month that has not happened yet is <c>null</c>.
/// </summary>
public static class CohortRetentionBuilder
{
    public static IReadOnlyList<ReportRow> Build(
        IEnumerable<ActivityEvent> invoices, IReadOnlyDictionary<Guid, DateTimeOffset> firstEver, string measure, DateOnly from, DateOnly to, DateOnly today, Func<DateTimeOffset, DateOnly> localDate)
    {
        static int MonthIndex(DateOnly d) => d.Year * 12 + d.Month - 1;

        var lineFilter = measure switch
        {
            "RepeatPurchase" => (Func<ActivityEvent, bool>)(e => e.BusinessLine == BusinessLine.Shop.Value),
            "RepeatVisit" => e => e.BusinessLine == BusinessLine.Clinic.Value,
            _ => _ => true,
        };

        var activeMonths = invoices.Where(e => !e.IsCredit && lineFilter(e)).GroupBy(e => e.CustomerId)
            .ToDictionary(g => g.Key, g => g.Select(e => MonthIndex(localDate(e.At))).ToHashSet());

        var rows = new List<ReportRow>();
        var firstMonth = MonthIndex(from);
        var lastMonth = MonthIndex(to);

        for (var month = firstMonth; month <= lastMonth; month++)
        {
            var cohort = firstEver.Where(f => MonthIndex(localDate(f.Value)) == month).Select(f => f.Key).ToList();
            if (cohort.Count == 0) continue;

            var row = new ReportRow { ["cohortMonth"] = $"{month / 12:0000}-{month % 12 + 1:00}", ["cohortSize"] = (long)cohort.Count };
            for (var k = 1; k <= 12; k++)
            {
                if (month + k > MonthIndex(today)) { row[$"month{k}"] = null; continue; }

                var retained = cohort.Count(c => activeMonths.TryGetValue(c, out var months) && months.Contains(month + k));
                row[$"month{k}"] = ReportMath.Percent(retained, cohort.Count);
            }

            rows.Add(row);
        }

        return rows;
    }
}
