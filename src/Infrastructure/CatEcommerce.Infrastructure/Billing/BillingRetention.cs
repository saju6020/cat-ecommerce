using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>
/// HO-BIL-001 ADR-192 / HO-XCT-001 §5.2 C-ALL-2. Billing stores a customer's personal data beyond the id in exactly
/// one place: the <b>bill-to snapshot</b> on an issued invoice (name, mobile, email, address), plus free-text notes on a
/// payment. Every such record is <c>Financial</c> / <c>Forbidden</c> (D-9: retained per tax law), so this eraser <b>mutates
/// nothing</b> -- it reports <see cref="ErasureOutcome.Held"/> with the earliest date the retained data may be reviewed,
/// or <see cref="ErasureOutcome.Completed"/> when the customer has none (or every held record is past its period, which is
/// a review flag only -- never an automatic delete, ADR-301).
///
/// <b>Retention period.</b> HO-XCT-001's <c>retention.financialYears</c> setting does not exist yet; until it lands this
/// uses the same fixed default as <c>AdministrationPersonalDataEraser</c> (D-9 "assumed 6 years") -- rewiring is
/// HO-XCT-001's own change.
/// </summary>
public sealed class BillingPersonalDataEraser(CatEcommerceDbContext db) : IPersonalDataEraser
{
    public const int DefaultRetentionYears = BillingRetention.DefaultYears;

    public string ContextKey => "billing";

    public async Task<ErasureOutcome> EraseAsync(Guid customerAccountId, DateTimeOffset asOf, UserContext actor, CancellationToken ct)
    {
        var heldDates = new List<DateOnly>();

        var invoiceDate = await db.Set<InvoiceAggregate>().AsNoTracking()
            .Where(i => i.CustomerId == customerAccountId && i.BillTo != null && i.IssueDate != null)
            .MaxAsync(i => i.IssueDate, ct);
        if (invoiceDate is { } issued) heldDates.Add(issued);

        heldDates.AddRange(await BillingRetention.OtherPersonalDataDatesAsync(db, customerAccountId, ct));

        if (heldDates.Count == 0) return new ErasureOutcome.Completed();

        var until = heldDates.Max().AddYears(DefaultRetentionYears);
        var today = DateOnly.FromDateTime(asOf.UtcDateTime);
        return until > today ? new ErasureOutcome.Held(until, "D-9 financial record") : new ErasureOutcome.Completed();
    }
}

/// <summary>HO-XCT-001 §5.2 C-ALL-3 / §8.10 -- how many Billing financial records are past the retention period (D-9). A
/// <b>review</b> count only: nothing is ever deleted or anonymised automatically (ADR-301).</summary>
public sealed class BillingRetentionReviewSource(CatEcommerceDbContext db) : IRetentionReviewSource
{
    public string SourceKey => "billing-financial";

    public async Task<IReadOnlyList<RetentionReviewCount>> CountPastRetentionAsync(DateOnly asOf, CancellationToken ct)
    {
        var cutoff = asOf.AddYears(-BillingRetention.DefaultYears);
        var counts = new List<RetentionReviewCount>();

        var invoices = db.Set<InvoiceAggregate>().AsNoTracking().Where(i => i.IssueDate != null && i.IssueDate <= cutoff);
        var invoiceCount = await invoices.CountAsync(ct);
        if (invoiceCount > 0)
            counts.Add(new RetentionReviewCount(RecordClass.Financial, "invoice", invoiceCount, await invoices.MinAsync(i => i.IssueDate, ct)));

        counts.AddRange(await BillingRetention.OtherCountsAsync(db, cutoff, ct));
        return counts;
    }
}

internal static class BillingRetention
{
    public const int DefaultYears = 6;

    /// <summary>Besides the invoice's bill-to snapshot, the only free text about a customer Billing retains is a payment's notes
    /// (a payment is Forbidden-to-delete, D-9): those payments' dates hold the customer's data until their period ends.</summary>
    public static async Task<List<DateOnly>> OtherPersonalDataDatesAsync(CatEcommerceDbContext db, Guid customerId, CancellationToken ct)
    {
        var dates = new List<DateOnly>();

        var noted = await db.Set<PaymentAggregate>().AsNoTracking()
            .Where(p => p.CustomerId == customerId && p.Notes != null)
            .MaxAsync(p => (DateOnly?)p.ReceivedOn, ct);
        if (noted is { } received) dates.Add(received);

        // A credit note and a refund carry a staff-written reason that may name the customer; both are Forbidden-to-delete (D-9).
        var creditNoted = await db.Set<CreditNoteAggregate>().AsNoTracking().Where(c => c.CustomerId == customerId).MaxAsync(c => (DateOnly?)c.IssueDate, ct);
        if (creditNoted is { } creditDate) dates.Add(creditDate);

        var refunded = await db.Set<RefundAggregate>().AsNoTracking().Where(r => r.CustomerId == customerId).MaxAsync(r => (DateOnly?)r.BusinessDate, ct);
        if (refunded is { } refundDate) dates.Add(refundDate);

        return dates;
    }

    public static async Task<List<RetentionReviewCount>> OtherCountsAsync(CatEcommerceDbContext db, DateOnly cutoff, CancellationToken ct)
    {
        var counts = new List<RetentionReviewCount>();

        var payments = db.Set<PaymentAggregate>().AsNoTracking().Where(p => p.ReceivedOn <= cutoff);
        var paymentCount = await payments.CountAsync(ct);
        if (paymentCount > 0)
            counts.Add(new RetentionReviewCount(RecordClass.Financial, "payment", paymentCount, await payments.MinAsync(p => (DateOnly?)p.ReceivedOn, ct)));

        await AddAsync("credit_note", db.Set<CreditNoteAggregate>().AsNoTracking().Where(c => c.IssueDate <= cutoff).Select(c => (DateOnly?)c.IssueDate));
        await AddAsync("refund", db.Set<RefundAggregate>().AsNoTracking().Where(r => r.BusinessDate <= cutoff).Select(r => (DateOnly?)r.BusinessDate));
        await AddAsync("cash_up", db.Set<CashUpAggregate>().AsNoTracking().Where(c => c.BusinessDate <= cutoff).Select(c => (DateOnly?)c.BusinessDate));
        await AddAsync("cod_remittance", db.Set<CodRemittanceAggregate>().AsNoTracking().Where(r => r.ReceivedOn <= cutoff).Select(r => (DateOnly?)r.ReceivedOn));
        await AddAsync("bank_statement", db.Set<BankStatementAggregate>().AsNoTracking().Where(s => s.PeriodTo <= cutoff).Select(s => (DateOnly?)s.PeriodTo));

        async Task AddAsync(string recordType, IQueryable<DateOnly?> dates)
        {
            var count = await dates.CountAsync(ct);
            if (count > 0) counts.Add(new RetentionReviewCount(RecordClass.Financial, recordType, count, await dates.MinAsync(ct)));
        }

        return counts;
    }
}
