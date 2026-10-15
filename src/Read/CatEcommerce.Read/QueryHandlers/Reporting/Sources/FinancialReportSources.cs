using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Sources;

/// <summary>The invoice states that count as recognised revenue (ADR-283): issued, part paid, paid, credited. A draft or a discarded invoice never counts.</summary>
public static class RevenueStatuses
{
    public static readonly int[] Counted = [InvoiceStatus.Issued.Value, InvoiceStatus.PartPaid.Value, InvoiceStatus.Credited.Value, InvoiceStatus.Paid.Value];
}

/// <summary>
/// Loads the revenue events of a report range from the views: invoice lines issued in the range, credit note lines dated in it and -- when the caller may see cost -- the stock ledger entries behind the
/// shop lines, so <see cref="LineCosting"/> can cost them. Everything is a read of the report views; the arithmetic is the pure <see cref="RevenueBuilder"/>.
/// </summary>
internal static class RevenueLoader
{
    public static async Task<IReadOnlyList<RevenueFact>> LoadAsync(CatEcommerceDbContext db, ReportRequest request, bool shopOnly, bool includeCost, CancellationToken ct)
    {
        var counted = RevenueStatuses.Counted;
        var shop = BusinessLine.Shop.Value;

        var lineQuery = db.Set<RvInvoiceLineRow>().AsNoTracking().Where(l => l.IssuedOn >= request.StartInclusive && l.IssuedOn < request.EndExclusive && counted.Contains(l.InvoiceStatus));
        var creditQuery = db.Set<RvCreditNoteLineRow>().AsNoTracking().Where(l => l.IssuedOn >= request.StartInclusive && l.IssuedOn < request.EndExclusive);
        if (!request.AllBranches)
        {
            lineQuery = lineQuery.Where(l => l.BranchId == request.BranchId);
            creditQuery = creditQuery.Where(l => l.BranchId == request.BranchId);
        }

        if (shopOnly)
        {
            lineQuery = lineQuery.Where(l => l.BusinessLine == shop);
            creditQuery = creditQuery.Where(l => l.BusinessLine == shop);
        }

        var lines = await lineQuery.ToListAsync(ct);
        var credits = await creditQuery.ToListAsync(ct);

        // Returns in the range reduce the cost of goods on THEIR date; they belong to lines that may have been invoiced earlier.
        var returnLines = new List<RvInvoiceLineRow>();
        var costs = new Dictionary<Guid, LineCost>();
        if (includeCost)
        {
            var returnKind = MovementKind.Return.Value;
            var returned = await db.Set<RvStockLedgerRow>().AsNoTracking()
                .Where(e => e.MovementKind == returnKind && e.OccurredOn >= request.StartInclusive && e.OccurredOn < request.EndExclusive && e.SourceLineId != null)
                .Select(e => e.SourceLineId!.Value).Distinct().ToListAsync(ct);

            if (returned.Count > 0)
            {
                var returnQuery = db.Set<RvInvoiceLineRow>().AsNoTracking().Where(l => l.SourceLineId != null && returned.Contains(l.SourceLineId.Value) && counted.Contains(l.InvoiceStatus));
                if (!request.AllBranches) returnQuery = returnQuery.Where(l => l.BranchId == request.BranchId);
                if (shopOnly) returnQuery = returnQuery.Where(l => l.BusinessLine == shop);
                returnLines = await returnQuery.ToListAsync(ct);
            }

            costs = await CostsAsync(db, lines.Select(l => l.SourceLineId).Concat(credits.Select(c => c.SourceLineId)).Concat(returnLines.Select(l => l.SourceLineId)), ct);
        }

        var skuIds = lines.Select(l => l.SkuId).Concat(credits.Select(c => c.SkuId)).Concat(returnLines.Select(l => l.SkuId)).OfType<Guid>().Distinct().ToList();
        var skus = await db.Set<RvSkuRow>().AsNoTracking().Where(s => skuIds.Contains(s.SkuId)).ToDictionaryAsync(s => s.SkuId, ct);

        RvSkuRow? Sku(Guid? id) => id is { } key ? skus.GetValueOrDefault(key) : null;
        LineCost Cost(Guid? sourceLineId) => sourceLineId is { } key ? costs.GetValueOrDefault(key, LineCost.Uncosted) : LineCost.Uncosted;

        var facts = new List<RevenueFact>();
        foreach (var l in lines)
            facts.Add(RevenueFacts.FromInvoiceLine(l, Sku(l.SkuId), Cost(l.SourceLineId), request.LocalDate(l.IssuedOn!.Value)));

        foreach (var c in credits)
            facts.Add(RevenueFacts.FromCreditLine(c, Sku(c.SkuId), Cost(c.SourceLineId), request.LocalDate(c.IssuedOn)));

        foreach (var l in returnLines)
        {
            foreach (var (at, cost) in Cost(l.SourceLineId).Returns.Where(r => r.At >= request.StartInclusive && r.At < request.EndExclusive))
                facts.Add(RevenueFacts.ReturnCost(l, Sku(l.SkuId), cost, request.LocalDate(at), l.CurrencyCode));
        }

        return facts;
    }

    /// <summary>The cost of every ledger-linked line: the sale-type and return entries that carry the line's id, with each batch's landed cost.</summary>
    private static async Task<Dictionary<Guid, LineCost>> CostsAsync(CatEcommerceDbContext db, IEnumerable<Guid?> sourceLineIds, CancellationToken ct)
    {
        var ids = sourceLineIds.OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0) return [];

        var kinds = new[] { MovementKind.Sale.Value, MovementKind.Dispense.Value, MovementKind.BoardingUse.Value, MovementKind.Return.Value };
        var entries = await (
            from e in db.Set<RvStockLedgerRow>().AsNoTracking()
            join b in db.Set<RvBatchRow>().AsNoTracking() on e.BatchId equals b.BatchId
            where e.SourceLineId != null && ids.Contains(e.SourceLineId.Value) && kinds.Contains(e.MovementKind)
            select new { Line = e.SourceLineId!.Value, e.MovementKind, e.QuantityDelta, e.OccurredOn, b.LandedUnitCost }).ToListAsync(ct);

        return entries.GroupBy(e => e.Line)
            .ToDictionary(g => g.Key, g => LineCosting.Cost(g.Select(e => new LedgerCostEntry(e.MovementKind, e.QuantityDelta, e.OccurredOn, e.LandedUnitCost))));
    }
}

internal static class RevenueRows
{
    public static ReportRow Row(RevenueAggregate a, string[] dimensionKeys, bool includeCost, bool withCreditColumns, string quantityKey)
    {
        var row = new ReportRow { ["bucket"] = a.Bucket };
        for (var i = 0; i < dimensionKeys.Length; i++) row[dimensionKeys[i]] = a.Dimensions[i];

        if (withCreditColumns)
        {
            row["gross"] = RevenueBuilder.Money(a.Gross, a.Currency);
            row["discounts"] = RevenueBuilder.Money(a.Discounts, a.Currency);
            row["creditNotes"] = RevenueBuilder.Money(a.CreditNotes, a.Currency);
            row["tax"] = RevenueBuilder.Money(a.Tax, a.Currency);
        }
        else
        {
            row[quantityKey] = a.Quantity;
        }

        row["netRevenue"] = RevenueBuilder.Money(a.Net, a.Currency);
        if (!includeCost) return row;

        row["cogs"] = a.CostedNet > 0m ? RevenueBuilder.Money(a.Cogs, a.Currency) : null;
        row["margin"] = RevenueBuilder.Money(a.Margin, a.Currency);
        row["marginPercent"] = a.MarginPercent;
        row["coveragePercent"] = a.CoveragePercent;
        return row;
    }

    public static ReportRow Totals(IReadOnlyList<RevenueAggregate> aggregates, bool includeCost, bool withCreditColumns, string quantityKey, string firstKey)
    {
        var totals = new ReportRow { [firstKey] = "Total" };
        if (aggregates.Count == 0 || aggregates.Select(a => a.Currency).Distinct().Count() != 1) return totals;

        var currency = aggregates[0].Currency;
        var sum = new RevenueAggregate("", [], currency, aggregates.Sum(a => a.Quantity), aggregates.Sum(a => a.Gross), aggregates.Sum(a => a.Discounts), aggregates.Sum(a => a.CreditNotes),
            aggregates.Sum(a => a.Net), aggregates.Sum(a => a.Tax), aggregates.Sum(a => a.Cogs), aggregates.Sum(a => a.CostedNet));

        var row = Row(sum, [], includeCost, withCreditColumns, quantityKey);
        row.Remove("bucket");
        foreach (var (key, value) in row) totals[key] = value;
        totals[firstKey] = "Total";
        return totals;
    }

    public static IReadOnlyList<ReportNote> Notes(IReadOnlyList<RevenueAggregate> aggregates, bool includeCost, string scope, bool clinicNotes)
    {
        var notes = new List<ReportNote>();
        if (includeCost && RevenueBuilder.CoverageNote(RevenueBuilder.Coverage(aggregates), scope) is { } coverage) notes.Add(coverage);

        if (clinicNotes)
        {
            notes.Add(new ReportNote("ClinicVisitChargesNotDelivered", ReportingMessages.ClinicVisitChargesNotDelivered));
            if (aggregates.Any(a => a.Dimensions.Contains(nameof(BusinessLine.Clinic)))) notes.Add(new ReportNote("ServicesHaveNoCost", ReportingMessages.ServicesHaveNoCost));
        }

        return notes;
    }
}

/// <summary>R9 -- <c>revenue-by-business-line</c> (US-RPT-03 AC1, AC2). Revenue by the line HO-BIL-001 stored on each invoice line; margin only where the landed cost is known, with its coverage (ADR-283, ADR-284).</summary>
public sealed class RevenueByBusinessLineSource : IReportSource
{
    public string Key => ReportCatalogue.RevenueByBusinessLineKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var facts = await RevenueLoader.LoadAsync(db, request, shopOnly: false, includeCost: true, ct);
        var aggregates = RevenueBuilder.Aggregate(facts, request.GroupBy ?? TimeBucket.Day, f => [f.Line]);

        var lineOrder = new[] { nameof(BusinessLine.Clinic), nameof(BusinessLine.Shop), nameof(BusinessLine.Boarding), BusinessLineRules.Other };
        var ordered = aggregates.OrderBy(a => a.Bucket, StringComparer.Ordinal).ThenBy(a => Array.IndexOf(lineOrder, a.Dimensions[0])).ThenBy(a => a.Currency, StringComparer.Ordinal).ToList();
        var rows = ordered.Select(a => RevenueRows.Row(a, ["businessLine"], includeCost: true, withCreditColumns: true, "quantity")).ToList();

        return new ReportSourceResult(
            rows.Take(request.RowLimit).ToList(), RevenueRows.Totals(ordered, true, true, "quantity", "bucket"), RevenueRows.Notes(ordered, true, "total", clinicNotes: true), RevenueBuilder.Coverage(ordered));
    }
}

/// <summary>R10 -- <c>revenue-shop-by-category</c> (US-RPT-03 AC1, AC2). The shop line of R9, by product category and sub-category. Cost, margin and coverage are removed for anyone without the finance action.</summary>
public sealed class RevenueShopByCategorySource : IReportSource
{
    public string Key => ReportCatalogue.RevenueShopByCategoryKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var includeCost = request.IsVisible("cogs");
        var facts = await RevenueLoader.LoadAsync(db, request, shopOnly: true, includeCost, ct);
        var aggregates = RevenueBuilder.Aggregate(facts, request.GroupBy ?? TimeBucket.Day, f => [f.Category, f.SubCategory]);
        var rows = aggregates.Select(a => RevenueRows.Row(a, ["category", "subCategory"], includeCost, withCreditColumns: false, "units")).ToList();

        return new ReportSourceResult(
            rows.Take(request.RowLimit).ToList(), RevenueRows.Totals(aggregates, includeCost, false, "units", "bucket"), RevenueRows.Notes(aggregates, includeCost, "shop", clinicNotes: false),
            includeCost ? RevenueBuilder.Coverage(aggregates) : null);
    }
}

/// <summary>R11 -- <c>revenue-shop-by-origin</c> (US-RPT-03 AC1, AC2). The shop line of R9 by the product's country of origin; products with none are "Not applicable".</summary>
public sealed class RevenueShopByOriginSource : IReportSource
{
    public string Key => ReportCatalogue.RevenueShopByOriginKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var includeCost = request.IsVisible("cogs");
        var facts = await RevenueLoader.LoadAsync(db, request, shopOnly: true, includeCost, ct);
        var aggregates = RevenueBuilder.Aggregate(facts, request.GroupBy ?? TimeBucket.Day, f => [f.Origin]);
        var rows = aggregates.Select(a => RevenueRows.Row(a, ["origin"], includeCost, withCreditColumns: false, "units")).ToList();

        return new ReportSourceResult(
            rows.Take(request.RowLimit).ToList(), RevenueRows.Totals(aggregates, includeCost, false, "units", "bucket"), RevenueRows.Notes(aggregates, includeCost, "shop", clinicNotes: false),
            includeCost ? RevenueBuilder.Coverage(aggregates) : null);
    }
}

/// <summary>R12 -- <c>revenue-by-service</c> (SRS section 8.2): revenue by business line and service or product group.</summary>
public sealed class RevenueByServiceSource : IReportSource
{
    public string Key => ReportCatalogue.RevenueByServiceKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var facts = await RevenueLoader.LoadAsync(db, request, shopOnly: false, includeCost: true, ct);
        var aggregates = RevenueBuilder.Aggregate(facts, request.GroupBy ?? TimeBucket.Day, f => [f.Line, f.ServiceGroup]);
        var rows = aggregates.Select(a => RevenueRows.Row(a, ["businessLine", "group"], includeCost: true, withCreditColumns: false, "quantity")).ToList();
        foreach (var row in rows) row.Remove("marginPercent");

        return new ReportSourceResult(
            rows.Take(request.RowLimit).ToList(), RevenueRows.Totals(aggregates, true, false, "quantity", "bucket"), RevenueRows.Notes(aggregates, true, "total", clinicNotes: true), RevenueBuilder.Coverage(aggregates));
    }
}

/// <summary>R13 -- <c>payment-reconciliation</c> (SRS section 8.2, AC-8). The four launch methods only; no gateway.</summary>
public sealed class PaymentReconciliationSource : IReportSource
{
    public string Key => ReportCatalogue.PaymentReconciliationKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var accepted = PaymentStatus.Accepted.Value;
        var paymentQuery = db.Set<RvPaymentRow>().AsNoTracking().Where(p => p.Status == accepted && p.BusinessDate >= request.From && p.BusinessDate <= request.To);
        var cashUpQuery = db.Set<RvCashUpRow>().AsNoTracking().Where(c => c.BusinessDate >= request.From && c.BusinessDate <= request.To);
        if (!request.AllBranches)
        {
            paymentQuery = paymentQuery.Where(p => p.BranchId == request.BranchId);
            cashUpQuery = cashUpQuery.Where(c => c.BranchId == request.BranchId);
        }

        var payments = await paymentQuery.ToListAsync(ct);
        var cashUps = await cashUpQuery.ToListAsync(ct);
        var branchNames = await db.Set<RvBranchRow>().AsNoTracking().ToDictionaryAsync(b => b.BranchId, b => b.Name, ct);

        var (rows, notes) = PaymentReconciliationBuilder.Build(payments, cashUps, branchNames, request.CurrencyCode);
        return new ReportSourceResult(rows.Take(request.RowLimit).ToList(), null, notes);
    }
}

/// <summary>R14 -- <c>daily-cash-up</c> (SRS section 8.2, FR-BIL-13): what Billing recorded; an open day is "Not closed".</summary>
public sealed class DailyCashUpSource : IReportSource
{
    public string Key => ReportCatalogue.DailyCashUpKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var query = db.Set<RvCashUpRow>().AsNoTracking().Where(c => c.BusinessDate >= request.From && c.BusinessDate <= request.To);
        if (!request.AllBranches) query = query.Where(c => c.BranchId == request.BranchId);

        var cashUps = await query.ToListAsync(ct);
        var branchNames = await db.Set<RvBranchRow>().AsNoTracking().ToDictionaryAsync(b => b.BranchId, b => b.Name, ct);
        var staffIds = cashUps.Select(c => c.ClosedBy).OfType<Guid>().Distinct().ToList();
        var staffNames = await db.Set<RvStaffRow>().AsNoTracking().Where(s => staffIds.Contains(s.StaffAccountId)).ToDictionaryAsync(s => s.StaffAccountId, s => s.FullName, ct);

        var rows = CashUpReportBuilder.Build(cashUps, branchNames, staffNames, request.From, request.To, request.LocalDate(request.Now), request.AllBranches ? null : request.BranchId, request.CurrencyCode);
        return new ReportSourceResult(rows.Take(request.RowLimit).ToList());
    }
}

/// <summary>R15 -- <c>receivables-ageing</c> (SRS section 8.2): who owes what, aged from the issue date to the end of the range.</summary>
public sealed class ReceivablesAgeingSource : IReportSource
{
    public string Key => ReportCatalogue.ReceivablesAgeingKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var query = db.Set<RvInvoiceBalanceRow>().AsNoTracking().Where(i => i.OutstandingAmount > 0m && i.IssueDate <= request.To);
        if (!request.AllBranches) query = query.Where(i => i.BranchId == request.BranchId);

        var invoices = await query.ToListAsync(ct);
        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();

        // A merged customer is shown under the FINAL survivor: follow merged_into_customer_id through the whole chain (A -> B -> C), a level at a time (bounded, so a cycle cannot loop).
        var byId = new Dictionary<Guid, RvCustomerRow>();
        var pending = customerIds;
        for (var hop = 0; hop < 20 && pending.Count > 0; hop++)
        {
            var batch = await db.Set<RvCustomerRow>().AsNoTracking().Where(c => pending.Contains(c.CustomerId)).ToListAsync(ct);
            foreach (var customer in batch) byId[customer.CustomerId] = customer;
            pending = batch.Select(c => c.MergedIntoCustomerId).OfType<Guid>().Where(id => !byId.ContainsKey(id)).Distinct().ToList();
        }

        (Guid Id, string Name) CustomerOf(Guid id)
        {
            if (!byId.TryGetValue(id, out var current)) return (id, "Customer");
            for (var hop = 0; hop < 20 && current.MergedIntoCustomerId is { } next && byId.TryGetValue(next, out var survivor); hop++) current = survivor;
            return (current.CustomerId, current.DisplayName);
        }

        var rows = ReceivablesAgeingBuilder.Build(invoices, request.To, CustomerOf);
        var notes = new List<ReportNote>();
        if (request.To < request.LocalDate(request.Now))
            notes.Add(new ReportNote("BalanceAsAtNow", "Balances are as they stand now; the end of the range only sets the date the invoices are aged to."));

        return new ReportSourceResult(rows.Take(request.RowLimit).ToList(), ReceivablesAgeingBuilder.Totals(rows), notes);
    }
}

/// <summary>R16 -- <c>refunds-credit-notes</c> (SRS section 8.2, FR-ORD-17): credit notes by reason and how each was settled.</summary>
public sealed class RefundsCreditNotesSource : IReportSource
{
    public string Key => ReportCatalogue.RefundsCreditNotesKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var notesQuery = db.Set<RvCreditNoteRow>().AsNoTracking().Where(c => c.IssuedOn >= request.StartInclusive && c.IssuedOn < request.EndExclusive);
        if (!request.AllBranches) notesQuery = notesQuery.Where(c => c.BranchId == request.BranchId);

        var creditNotes = await notesQuery.ToListAsync(ct);
        var ids = creditNotes.Select(c => c.CreditNoteId).ToList();
        var refunds = await db.Set<RvRefundRow>().AsNoTracking().Where(r => r.CreditNoteId != null && ids.Contains(r.CreditNoteId.Value)).ToListAsync(ct);

        var rows = RefundsReportBuilder.Build(creditNotes, refunds);
        var totals = new ReportRow { ["reason"] = "Total", ["count"] = creditNotes.Count };
        if (RevenueBuilder.SumIfOneCurrency(rows.Select(r => r["amount"] as ReportMoney)) is { } sum) totals["amount"] = sum;

        return new ReportSourceResult(rows.Take(request.RowLimit).ToList(), totals);
    }
}

/// <summary>R17 -- <c>tax-summary</c> (SRS section 8.2): output tax by tax class and rate, credit notes as negative lines.</summary>
public sealed class TaxSummarySource : IReportSource
{
    public string Key => ReportCatalogue.TaxSummaryKey;

    public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
    {
        var counted = RevenueStatuses.Counted;
        var lineQuery = db.Set<RvInvoiceLineRow>().AsNoTracking().Where(l => l.IssuedOn >= request.StartInclusive && l.IssuedOn < request.EndExclusive && counted.Contains(l.InvoiceStatus));
        var creditQuery = db.Set<RvCreditNoteLineRow>().AsNoTracking().Where(l => l.IssuedOn >= request.StartInclusive && l.IssuedOn < request.EndExclusive);
        if (!request.AllBranches)
        {
            lineQuery = lineQuery.Where(l => l.BranchId == request.BranchId);
            creditQuery = creditQuery.Where(l => l.BranchId == request.BranchId);
        }

        var lines = (await lineQuery.ToListAsync(ct)).Select(l => (request.LocalDate(l.IssuedOn!.Value), l)).ToList();
        var credits = (await creditQuery.ToListAsync(ct)).Select(l => (request.LocalDate(l.IssuedOn), l)).ToList();

        var rows = TaxSummaryBuilder.Build(lines, credits, request.GroupBy ?? TimeBucket.Day);
        var totals = new ReportRow { ["bucket"] = "Total" };
        if (RevenueBuilder.SumIfOneCurrency(rows.Select(r => r["taxableNet"] as ReportMoney)) is { } net) totals["taxableNet"] = net;
        if (RevenueBuilder.SumIfOneCurrency(rows.Select(r => r["outputTax"] as ReportMoney)) is { } tax) totals["outputTax"] = tax;

        return new ReportSourceResult(rows.Take(request.RowLimit).ToList(), totals);
    }
}
