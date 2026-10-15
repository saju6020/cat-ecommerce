using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One outstanding issued invoice, as the ageing view needs it.</summary>
public sealed record AgeingInput(Guid CustomerId, Guid InvoiceId, DateOnly DueOn, decimal Outstanding);

public sealed record AgeingBuckets(decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal Over90)
{
    public static readonly AgeingBuckets Empty = new(0m, 0m, 0m, 0m, 0m);

    public decimal Total => Current + Days1To30 + Days31To60 + Days61To90 + Over90;

    public AgeingBuckets Add(AgeingBucket bucket, decimal amount)
    {
        if (bucket == AgeingBucket.Current) return this with { Current = Current + amount };
        if (bucket == AgeingBucket.Days1To30) return this with { Days1To30 = Days1To30 + amount };
        if (bucket == AgeingBucket.Days31To60) return this with { Days31To60 = Days31To60 + amount };
        if (bucket == AgeingBucket.Days61To90) return this with { Days61To90 = Days61To90 + amount };
        return this with { Over90 = Over90 + amount };
    }
}

public sealed record AgeingCustomerRow(Guid CustomerId, AgeingBuckets Buckets, DateOnly OldestDueOn);

/// <summary>HO-BIL-001 §12.6 (US-BIL-03 AC2) -- pure. Outstanding is computed per invoice in SQL; this
/// only buckets it by days overdue against <c>asOf</c> (branch-local today by default). Not yet due
/// (or due today) is <c>Current</c>.</summary>
public static class AgeingCalculator
{
    public static AgeingBucket BucketFor(DateOnly dueOn, DateOnly asOf)
    {
        var daysOverdue = asOf.DayNumber - dueOn.DayNumber;
        if (daysOverdue <= 0) return AgeingBucket.Current;
        if (daysOverdue <= 30) return AgeingBucket.Days1To30;
        if (daysOverdue <= 60) return AgeingBucket.Days31To60;
        if (daysOverdue <= 90) return AgeingBucket.Days61To90;
        return AgeingBucket.Over90;
    }

    public static AgeingBuckets Totals(IEnumerable<AgeingInput> inputs, DateOnly asOf) =>
        inputs.Aggregate(AgeingBuckets.Empty, (acc, i) => acc.Add(BucketFor(i.DueOn, asOf), i.Outstanding));

    public static IReadOnlyList<AgeingCustomerRow> ByCustomer(IEnumerable<AgeingInput> inputs, DateOnly asOf) =>
        inputs
            .GroupBy(i => i.CustomerId)
            .Select(g => new AgeingCustomerRow(g.Key, Totals(g, asOf), g.Min(i => i.DueOn)))
            .OrderBy(r => r.OldestDueOn).ThenBy(r => r.CustomerId)
            .ToList();
}
