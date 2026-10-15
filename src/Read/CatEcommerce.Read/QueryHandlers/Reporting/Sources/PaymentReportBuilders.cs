using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Sources;

/// <summary>
/// HO-RPT-001 section 8.3 R13 (SRS section 8.2, FR-BIL-7) -- the payment reconciliation per business day and method, <b>pure</b> over the accepted payments and the cash-ups. The methods are exactly
/// the four launch methods -- <c>CashOnDelivery</c>, <c>BankTransfer</c>, <c>CashInStore</c>, <c>CardInStore</c> (FR-ORD-8); there is no gateway (SRS section 1.2). Cash on delivery is compared with
/// courier remittances, bank transfers with the bank statement matches, cash in store with the till count. Card in store has no terminal settlement report to compare against (SRS Q-3c, OQ-17),
/// so it shows what was expected and nothing to compare it with.
/// </summary>
public static class PaymentReconciliationBuilder
{
    public const string CardNote = "No terminal settlement report to compare against (SRS Q-3c).";

    private static readonly string[] MethodOrder =
    [
        nameof(ReportPaymentMethods.CashOnDelivery), nameof(ReportPaymentMethods.BankTransfer), nameof(ReportPaymentMethods.CashInStore), nameof(ReportPaymentMethods.CardInStore)
    ];

    public static (IReadOnlyList<ReportRow> Rows, IReadOnlyList<ReportNote> Notes) Build(
        IEnumerable<RvPaymentRow> payments, IEnumerable<RvCashUpRow> cashUps, IReadOnlyDictionary<Guid, string> branchNames, string baseCurrency)
    {
        var accepted = payments.Where(p => p.Status == PaymentStatus.Accepted.Value && p.BusinessDate is not null).ToList();
        var rows = new List<(DateOnly Date, string Branch, int Order, ReportRow Row)>();
        var sawCard = false;

        // Cash on delivery: courier-collected payments against the remittances that cover them.
        foreach (var group in accepted.Where(p => p.Method == ReportPaymentMethods.CashOnDelivery.Value && p.CollectorKind == CollectorKind.Courier.Value).GroupBy(p => (p.BusinessDate!.Value, p.BranchId, p.CurrencyCode)))
        {
            var expected = group.Sum(p => p.Amount);
            var received = group.Sum(p => Math.Min(p.RemittedAmount, p.Amount));
            rows.Add((group.Key.Item1, Branch(branchNames, group.Key.BranchId), 0, Row(group.Key.Item1, branchNames, group.Key.BranchId, ReportPaymentMethods.CashOnDelivery, expected, received, group.Count(p => p.RemittedAmount >= p.Amount),
                group.Count(p => p.RemittedAmount < p.Amount), group.Key.CurrencyCode)));
        }

        // Bank transfer: accepted transfers against the bank statement lines they are matched to.
        foreach (var group in accepted.Where(p => p.Method == ReportPaymentMethods.BankTransfer.Value).GroupBy(p => (p.BusinessDate!.Value, p.BranchId, p.CurrencyCode)))
        {
            var expected = group.Sum(p => p.Amount);
            var received = group.Where(p => p.BankMatched).Sum(p => p.Amount);
            rows.Add((group.Key.Item1, Branch(branchNames, group.Key.BranchId), 1, Row(group.Key.Item1, branchNames, group.Key.BranchId, ReportPaymentMethods.BankTransfer, expected, received,
                group.Count(p => p.BankMatched), group.Count(p => !p.BankMatched), group.Key.CurrencyCode)));
        }

        // Cash in store: the till count of the day's cash-up, or the day's cash takings when the day has not been counted.
        var cashUpByDay = cashUps.ToDictionary(c => (c.BusinessDate, c.BranchId));
        var cashPayments = accepted.Where(p => p.Method == ReportPaymentMethods.CashInStore.Value).GroupBy(p => (p.BusinessDate!.Value, p.BranchId, p.CurrencyCode)).ToList();
        var cashKeys = cashPayments.Select(g => (g.Key.Item1, g.Key.BranchId)).Union(cashUpByDay.Keys).Distinct();

        foreach (var key in cashKeys)
        {
            var currency = cashPayments.FirstOrDefault(g => g.Key.Item1 == key.Item1 && g.Key.BranchId == key.BranchId)?.Key.CurrencyCode ?? baseCurrency;
            var takings = cashPayments.Where(g => g.Key.Item1 == key.Item1 && g.Key.BranchId == key.BranchId).Sum(g => g.Sum(p => p.Amount));

            if (cashUpByDay.TryGetValue(key, out var cashUp) && cashUp.ExpectedCash is { } expected)
            {
                var counted = cashUp.CountedCash;
                var variance = cashUp.Variance;
                var matched = counted is not null && variance == 0m ? 1 : 0;
                var open = counted is not null && variance != 0m ? 1 : 0;
                rows.Add((key.Item1, Branch(branchNames, key.BranchId), 2, new ReportRow
                {
                    ["businessDate"] = key.Item1, ["branchName"] = Branch(branchNames, key.BranchId), ["method"] = nameof(ReportPaymentMethods.CashInStore),
                    ["expected"] = new ReportMoney(expected, currency), ["received"] = counted is { } c ? new ReportMoney(c, currency) : null,
                    ["variance"] = variance is { } v ? new ReportMoney(v, currency) : null, [VarianceDirections.ColumnKey] = VarianceDirections.Name(counted, expected), ["matched"] = (long)matched, ["mismatchesOpen"] = (long)open,
                }));
            }
            else if (takings != 0m)
            {
                rows.Add((key.Item1, Branch(branchNames, key.BranchId), 2, new ReportRow
                {
                    ["businessDate"] = key.Item1, ["branchName"] = Branch(branchNames, key.BranchId), ["method"] = nameof(ReportPaymentMethods.CashInStore),
                    ["expected"] = new ReportMoney(takings, currency), ["received"] = null, ["variance"] = null, [VarianceDirections.ColumnKey] = null, ["matched"] = 0L, ["mismatchesOpen"] = 0L,
                }));
            }
        }

        // Card in store: expected only.
        foreach (var group in accepted.Where(p => p.Method == ReportPaymentMethods.CardInStore.Value).GroupBy(p => (p.BusinessDate!.Value, p.BranchId, p.CurrencyCode)))
        {
            sawCard = true;
            rows.Add((group.Key.Item1, Branch(branchNames, group.Key.BranchId), 3, new ReportRow
            {
                ["businessDate"] = group.Key.Item1, ["branchName"] = Branch(branchNames, group.Key.BranchId), ["method"] = nameof(ReportPaymentMethods.CardInStore),
                ["expected"] = new ReportMoney(group.Sum(p => p.Amount), group.Key.CurrencyCode), ["received"] = null, ["variance"] = null, [VarianceDirections.ColumnKey] = null, ["matched"] = 0L, ["mismatchesOpen"] = 0L,
            }));
        }

        var ordered = rows.OrderBy(r => r.Date).ThenBy(r => r.Branch, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Order).Select(r => r.Row).ToList();
        var notes = new List<ReportNote>();
        if (sawCard) notes.Add(new ReportNote("CardNoSettlement", CardNote));
        return (ordered, notes);
    }

    private static string Branch(IReadOnlyDictionary<Guid, string> names, Guid id) => names.GetValueOrDefault(id) ?? string.Empty;

    private static ReportRow Row(DateOnly date, IReadOnlyDictionary<Guid, string> names, Guid branchId, PaymentMethod method, decimal expected, decimal received, int matched, int open, string currency) => new()
    {
        ["businessDate"] = date,
        ["branchName"] = Branch(names, branchId),
        ["method"] = method.DisplayName,
        ["expected"] = new ReportMoney(expected, currency),
        ["received"] = new ReportMoney(received, currency),
        ["variance"] = new ReportMoney(expected - received, currency),
        [VarianceDirections.ColumnKey] = VarianceDirections.Name(received, expected),
        ["matched"] = (long)matched,
        ["mismatchesOpen"] = (long)open,
    };
}

/// <summary>HO-RPT-001 section 8.3 R14 (FR-BIL-13) -- the daily cash-up as Billing recorded it, <b>pure</b>. A day with no cash-up is "Not closed": its figures are blank, never zero.</summary>
public static class CashUpReportBuilder
{
    public const string NotClosed = "Not closed";

    public static IReadOnlyList<ReportRow> Build(
        IEnumerable<RvCashUpRow> cashUps, IReadOnlyDictionary<Guid, string> branchNames, IReadOnlyDictionary<Guid, string> staffNames, DateOnly from, DateOnly to, DateOnly today,
        Guid? singleBranchId, string currency)
    {
        var records = cashUps.ToDictionary(c => (c.BusinessDate, c.BranchId));
        var rows = new List<ReportRow>();

        // One branch: every day up to today appears, closed or not. All branches: only the days that have a cash-up.
        var keys = singleBranchId is { } branch
            ? Enumerable.Range(0, Math.Max(0, to.DayNumber - from.DayNumber + 1)).Select(i => from.AddDays(i)).Where(d => d <= today).Select(d => (d, branch))
            : records.Keys.Select(k => (k.BusinessDate, k.BranchId));

        foreach (var key in keys.OrderBy(k => k.Item1).ThenBy(k => branchNames.GetValueOrDefault(k.Item2)))
        {
            if (!records.TryGetValue((key.Item1, key.Item2), out var c))
            {
                rows.Add(new ReportRow
                {
                    ["businessDate"] = key.Item1, ["branchName"] = branchNames.GetValueOrDefault(key.Item2), ["status"] = NotClosed,
                    ["cash"] = null, ["cardInStore"] = null, ["bankTransfer"] = null, ["expected"] = null, ["counted"] = null, ["variance"] = null, [VarianceDirections.ColumnKey] = null, ["explanation"] = null, ["closedBy"] = null,
                });
                continue;
            }

            ReportMoney? Money(decimal? value) => value is { } v ? new ReportMoney(v, currency) : null;
            rows.Add(new ReportRow
            {
                ["businessDate"] = c.BusinessDate,
                ["branchName"] = branchNames.GetValueOrDefault(c.BranchId),
                ["status"] = ReportLabels.Of<CashUpStatus>(c.Status),
                ["cash"] = Money(c.CashRecorded), ["cardInStore"] = Money(c.CardRecorded), ["bankTransfer"] = Money(c.TransferRecorded),
                ["expected"] = Money(c.ExpectedCash), ["counted"] = Money(c.CountedCash), ["variance"] = Money(c.Variance), [VarianceDirections.ColumnKey] = VarianceDirections.Name(c.CountedCash, c.ExpectedCash),
                ["explanation"] = c.Explanation,
                ["closedBy"] = c.ClosedBy is { } by ? staffNames.GetValueOrDefault(by) : null,
            });
        }

        return rows;
    }
}

/// <summary>
/// HO-RPT-001 section 8.3 R15 -- receivables ageing, <b>pure</b>. The age is the days from the invoice's issue date to the end of the range (buckets 0-30, 31-60, 61-90, over 90); a customer merged into
/// another is shown under the survivor (HO-BIL-001 keeps issued invoices on the original customer id). Each currency is its own group.
/// </summary>
public static class ReceivablesAgeingBuilder
{
    public static string BucketOf(int ageDays) => ageDays <= 30 ? "0-30" : ageDays <= 60 ? "31-60" : ageDays <= 90 ? "61-90" : "over90";

    public static IReadOnlyList<ReportRow> Build(IEnumerable<RvInvoiceBalanceRow> invoices, DateOnly asOf, Func<Guid, (Guid Id, string Name)> customerOf)
    {
        var rows = new List<(decimal Balance, ReportRow Row)>();

        var open = invoices.Where(i => i.OutstandingAmount > 0m && i.IssueDate is { } issued && issued <= asOf)
            .Select(i => (Invoice: i, Customer: customerOf(i.CustomerId), Age: asOf.DayNumber - i.IssueDate!.Value.DayNumber));

        foreach (var group in open.GroupBy(x => (x.Customer.Id, x.Invoice.CurrencyCode)))
        {
            decimal Sum(string bucket) => group.Where(x => BucketOf(x.Age) == bucket).Sum(x => x.Invoice.OutstandingAmount);
            var currency = group.Key.CurrencyCode;
            var balance = group.Sum(x => x.Invoice.OutstandingAmount);

            rows.Add((balance, new ReportRow
            {
                ["customerName"] = group.First().Customer.Name,
                ["invoicesOutstanding"] = (long)group.Count(),
                ["balance"] = new ReportMoney(balance, currency),
                ["age0To30"] = new ReportMoney(Sum("0-30"), currency),
                ["age31To60"] = new ReportMoney(Sum("31-60"), currency),
                ["age61To90"] = new ReportMoney(Sum("61-90"), currency),
                ["ageOver90"] = new ReportMoney(Sum("over90"), currency),
            }));
        }

        return rows.OrderByDescending(r => r.Balance).Select(r => r.Row).ToList();
    }

    public static ReportRow Totals(IReadOnlyList<ReportRow> rows)
    {
        var totals = new ReportRow { ["customerName"] = $"{rows.Count} customers", ["invoicesOutstanding"] = rows.Sum(r => (long)(r["invoicesOutstanding"] ?? 0L)) };
        foreach (var key in new[] { "balance", "age0To30", "age31To60", "age61To90", "ageOver90" })
            if (RevenueBuilder.SumIfOneCurrency(rows.Select(r => r[key] as ReportMoney)) is { } sum) totals[key] = sum;
        return totals;
    }
}

/// <summary>HO-RPT-001 section 8.3 R16 (FR-ORD-17) -- credit notes by reason and how each was settled, <b>pure</b>. Refund methods are exactly cash in store and bank transfer; what was kept as customer credit and what simply reduced the invoice balance are their own lines.</summary>
public static class RefundsReportBuilder
{
    public const string CustomerCredit = "Customer credit";
    public const string AppliedToBalance = "Applied to the invoice balance";

    public static IReadOnlyList<ReportRow> Build(IReadOnlyList<RvCreditNoteRow> creditNotes, IReadOnlyList<RvRefundRow> refunds)
    {
        var settled = new List<(string Reason, string Method, string Currency, Guid CreditNoteId, decimal Amount)>();
        var kindOf = creditNotes.ToDictionary(c => c.CreditNoteId, c => ReportLabels.Of<CreditNoteKind>(c.CreditNoteKind));

        foreach (var refund in refunds.Where(r => r.CreditNoteId is not null && kindOf.ContainsKey(r.CreditNoteId.Value)))
            settled.Add((kindOf[refund.CreditNoteId!.Value], ReportLabels.Of<RefundMethod>(refund.Method), refund.CurrencyCode, refund.CreditNoteId.Value, refund.Amount));

        foreach (var note in creditNotes)
        {
            var reason = kindOf[note.CreditNoteId];
            var refunded = refunds.Where(r => r.CreditNoteId == note.CreditNoteId).Sum(r => r.Amount);
            if (note.ReleasedToCustomerCredit > 0m) settled.Add((reason, CustomerCredit, note.CurrencyCode, note.CreditNoteId, note.ReleasedToCustomerCredit));

            var remainder = note.Total - refunded - note.ReleasedToCustomerCredit;
            if (remainder > 0m) settled.Add((reason, AppliedToBalance, note.CurrencyCode, note.CreditNoteId, remainder));
        }

        return settled.GroupBy(s => (s.Reason, s.Method, s.Currency)).OrderBy(g => g.Key.Reason, StringComparer.Ordinal).ThenBy(g => g.Key.Method, StringComparer.Ordinal)
            .Select(g => new ReportRow
            {
                ["reason"] = g.Key.Reason, ["refundMethod"] = g.Key.Method, ["count"] = (long)g.Select(s => s.CreditNoteId).Distinct().Count(), ["amount"] = new ReportMoney(g.Sum(s => s.Amount), g.Key.Currency),
            }).ToList<ReportRow>();
    }
}

/// <summary>HO-RPT-001 section 8.3 R17 -- the tax summary, <b>pure</b>. Output tax by tax class and rate on issued invoices; credit notes are separate NEGATIVE lines on their own dates. The invoice lines' output tax sums to the tax on the invoices (I-D6).</summary>
public static class TaxSummaryBuilder
{
    public static IReadOnlyList<ReportRow> Build(IEnumerable<(DateOnly Date, RvInvoiceLineRow Line)> invoiceLines, IEnumerable<(DateOnly Date, RvCreditNoteLineRow Line)> creditLines, TimeBucket bucket)
    {
        var rows = new List<(string Bucket, int Order, string TaxClass, decimal Rate, string Currency, ReportRow Row)>();

        foreach (var group in invoiceLines.GroupBy(l => (Bucket: ReportBuckets.Label(l.Date, bucket), Class: l.Line.TaxClassCode ?? "-", l.Line.TaxRatePercent, l.Line.CurrencyCode)))
            rows.Add((group.Key.Bucket, 0, group.Key.Class, group.Key.TaxRatePercent, group.Key.CurrencyCode, Row(group.Key.Bucket, "Invoices", group.Key.Class, group.Key.TaxRatePercent,
                group.Sum(l => l.Line.NetAmount), group.Sum(l => l.Line.TaxAmount), group.Key.CurrencyCode)));

        foreach (var group in creditLines.GroupBy(l => (Bucket: ReportBuckets.Label(l.Date, bucket), Class: l.Line.TaxClassCode ?? "-", l.Line.TaxRatePercent, l.Line.CurrencyCode)))
            rows.Add((group.Key.Bucket, 1, group.Key.Class, group.Key.TaxRatePercent, group.Key.CurrencyCode, Row(group.Key.Bucket, "Credit notes", group.Key.Class, group.Key.TaxRatePercent,
                -group.Sum(l => l.Line.NetAmount), -group.Sum(l => l.Line.TaxAmount), group.Key.CurrencyCode)));

        return rows.OrderBy(r => r.Bucket, StringComparer.Ordinal).ThenBy(r => r.Order).ThenBy(r => r.TaxClass, StringComparer.Ordinal).ThenBy(r => r.Rate).ThenBy(r => r.Currency, StringComparer.Ordinal).Select(r => r.Row).ToList();
    }

    private static ReportRow Row(string bucket, string source, string taxClass, decimal rate, decimal net, decimal tax, string currency) => new()
    {
        ["bucket"] = bucket, ["source"] = source, ["taxClass"] = taxClass, ["ratePercent"] = rate, ["taxableNet"] = new ReportMoney(net, currency), ["outputTax"] = new ReportMoney(tax, currency),
    };
}

/// <summary>
/// The four launch payment methods, found through HO-BIL-001's <c>PaymentMethodRules</c> classification rather than named, so no Reporting code reads a payment-method identity directly (ADR-182,
/// BIL's U-X4). Bank transfer and cash on delivery have their own rule; cash and card in store are the two methods the till counts as a <c>Cash</c> or <c>Card</c> bucket without a collector.
/// </summary>
public static class ReportPaymentMethods
{
    private static readonly IReadOnlyList<PaymentMethod> All = Enumeration.GetAll<PaymentMethod>().ToList();

    public static readonly PaymentMethod CashOnDelivery = All.Single(PaymentMethodRules.IsCashOnDelivery);
    public static readonly PaymentMethod BankTransfer = All.Single(PaymentMethodRules.IsBankTransfer);
    public static readonly PaymentMethod CashInStore = All.Single(m => PaymentMethodRules.BucketOf(m, null) == TillBucket.Cash);
    public static readonly PaymentMethod CardInStore = All.Single(m => PaymentMethodRules.BucketOf(m, null) == TillBucket.Card);
}
