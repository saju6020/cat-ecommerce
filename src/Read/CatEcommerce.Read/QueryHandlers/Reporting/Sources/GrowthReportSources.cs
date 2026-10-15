using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Sources;

internal static class GrowthQueries
{
    /// <summary>The customer activity at the report's branch scope. Growth reports filter by the branch of the TRANSACTION (customers are not branch-scoped, section 8.7).</summary>
    public static IQueryable<RvCustomerActivityRow> Activity(CatEcommerceDbContext db, ReportRequest request)
    {
        var rows = db.Set<RvCustomerActivityRow>().AsNoTracking();
        return request.AllBranches ? rows : rows.Where(a => a.BranchId == request.BranchId);
    }

    public static ActivityEvent ToEvent(RvCustomerActivityRow r) => new(r.CustomerId, r.OccurredOn, r.BusinessLine, r.NetAmount, r.Kind == RvCustomerActivityRow.KindCreditNote);

    /// <summary>The first invoice ever of each of the customers, in the report's branch scope.</summary>
    public static async Task<Dictionary<Guid, DateTimeOffset>> FirstInvoiceAsync(CatEcommerceDbContext db, ReportRequest request, IReadOnlyCollection<Guid>? customers, CancellationToken ct)
    {
        var invoices = Activity(db, request).Where(a => a.Kind == RvCustomerActivityRow.KindInvoice);
        if (customers is not null) invoices = invoices.Where(a => customers.Contains(a.CustomerId));

        return await invoices.GroupBy(a => a.CustomerId).Select(g => new { Id = g.Key, First = g.Min(a => a.OccurredOn) }).ToDictionaryAsync(x => x.Id, x => x.First, ct);
    }
}

/// <summary>R18 -- <c>customers-new-vs-returning</c> (US-RPT-04, ADR-291).</summary>
public sealed class CustomersNewVsReturningSource : IReportSource
{
    public string Key => ReportCatalogue.CustomersNewVsReturningKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var invoiceKind = RvCustomerActivityRow.KindInvoice;
        var events = (await GrowthQueries.Activity(db, request)
            .Where(a => a.Kind == invoiceKind && a.OccurredOn >= request.StartInclusive && a.OccurredOn < request.EndExclusive).ToListAsync(ct)).Select(GrowthQueries.ToEvent).ToList();

        var ids = events.Select(e => e.CustomerId).Distinct().ToList();
        var firstEver = await GrowthQueries.FirstInvoiceAsync(db, request, ids, ct);
        var channels = await db.Set<RvCustomerRow>().AsNoTracking().Where(c => ids.Contains(c.CustomerId)).ToDictionaryAsync(c => c.CustomerId, c => c.RegistrationChannel, ct);
        var registrations = (await db.Set<RvCustomerRow>().AsNoTracking().Where(c => c.RegisteredOn >= request.StartInclusive && c.RegisteredOn < request.EndExclusive)
            .Select(c => new { c.RegisteredOn, c.RegistrationChannel }).ToListAsync(ct)).Select(c => (c.RegisteredOn, c.RegistrationChannel));

        var (rows, totals) = NewVersusReturningBuilder.Build(events, firstEver, channels, registrations, request.From, request.To, request.GroupBy ?? TimeBucket.Day, request.LocalDate);
        var notes = new List<ReportNote>
        {
            new("RegistrationsNotBranchScoped", "Registrations are not tied to a branch; they count every customer who registered in the range."),
        };
        return new ReportSourceResult(rows, totals, notes);
    }
}

/// <summary>R19 -- <c>customer-lifetime-value</c> (US-RPT-04).</summary>
public sealed class CustomerLifetimeValueSource : IReportSource
{
    public string Key => ReportCatalogue.CustomerLifetimeValueKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var firstEver = await GrowthQueries.FirstInvoiceAsync(db, request, null, ct);
        var cohort = firstEver.Where(f => f.Value >= request.StartInclusive && f.Value < request.EndExclusive).Select(f => f.Key).ToList();

        var events = (await GrowthQueries.Activity(db, request)
            .Where(a => cohort.Contains(a.CustomerId) && a.OccurredOn < request.EndExclusive && a.CurrencyCode == request.CurrencyCode).ToListAsync(ct)).Select(GrowthQueries.ToEvent).ToList();

        var names = await db.Set<RvCustomerRow>().AsNoTracking().Where(c => cohort.Contains(c.CustomerId)).ToDictionaryAsync(c => c.CustomerId, c => c.DisplayName, ct);
        var notes = new List<ReportNote> { new("LifetimeValueScope", "Lifetime value is the net revenue (invoices less credit notes) to the end of the range, in the base currency, for customers whose first invoice fell in the range.") };
        return new ReportSourceResult(LifetimeValueBuilder.Build(events, names, request.CurrencyCode), null, notes);
    }
}

/// <summary>R20 -- <c>cohort-retention</c> (US-RPT-04, OQ-8).</summary>
public sealed class CohortRetentionSource : IReportSource
{
    public string Key => ReportCatalogue.CohortRetentionKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var firstEver = await GrowthQueries.FirstInvoiceAsync(db, request, null, ct);
        var cohorts = firstEver.Where(f => f.Value >= request.StartInclusive && f.Value < request.EndExclusive).ToDictionary(f => f.Key, f => f.Value);
        var ids = cohorts.Keys.ToList();
        var invoiceKind = RvCustomerActivityRow.KindInvoice;

        var events = (await GrowthQueries.Activity(db, request).Where(a => a.Kind == invoiceKind && ids.Contains(a.CustomerId)).ToListAsync(ct)).Select(GrowthQueries.ToEvent).ToList();
        var rows = CohortRetentionBuilder.Build(events, cohorts, request.Text("measure"), request.From, request.To, request.LocalDate(request.Now), request.LocalDate);

        var notes = new List<ReportNote>
        {
            new("CohortDefinition", "A customer is retained in a month when they have an issued invoice (of the chosen kind) in that month. Months that have not happened yet are blank."),
        };
        return new ReportSourceResult(rows, null, notes);
    }
}
