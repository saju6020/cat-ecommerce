using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Billing.Export;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Billing;

/// <summary>
/// HO-BIL-001 ADR-190 -- reads only <b>immutable facts</b>, each dated by the date it happened: an issued invoice by its issue date, a payment by
/// the business date it was accepted (a reversal is its own fact on the reversal date), an allocation entry by its local date at the
/// payment's branch, a credit note by its issue date and a refund by its business date. Nothing mutable (status, paid, credited,
/// outstanding) is selected, so two runs over the same range return the same rows. Each list is capped just above
/// <c>MaxExportRows</c> so a runaway range cannot exhaust memory; the caller refuses an export over the cap.
/// </summary>
public sealed class GetAccountingExportContentQueryHandler(CatEcommerceDbContext db, IBusinessSettings settings)
    : IQueryHandlerAsync<GetAccountingExportContentQuery, AccountingExportContent>
{
    private const int Cap = BillingRules.MaxExportRows + 1;

    public async Task<QueryResponse<AccountingExportContent>> HandleAsync(GetAccountingExportContentQuery query)
    {
        var from = query.From;
        var to = query.To;
        var branchId = query.BranchId;

        var invoiceFilter = db.Set<InvoiceAggregate>().AsNoTracking()
            .Where(i => i.Number != null && i.IssueDate != null && i.IssueDate >= from && i.IssueDate <= to
                        && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartPaid || i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.Credited)
                        && (branchId == null || i.BranchId == branchId));

        var invoices = (await invoiceFilter.OrderBy(i => i.IssueDate).ThenBy(i => i.Number).Take(Cap)
            .Select(i => new
            {
                i.Id, i.Number, i.FinancialYear, i.BranchId, i.CustomerId, CustomerName = i.BillTo != null ? i.BillTo.FullName : string.Empty, i.IssueDate, i.DueOn,
                i.CurrencyCode, i.NetTotal, i.DiscountTotal, i.TaxTotal, i.Total, i.CorrectsInvoiceId,
            }).ToListAsync())
            .Select(i => new ExportInvoice(i.Id, i.Number!, i.FinancialYear ?? string.Empty, i.BranchId, i.CustomerId, i.CustomerName, i.IssueDate!.Value, i.DueOn, i.CurrencyCode,
                i.NetTotal, i.DiscountTotal, i.TaxTotal, i.Total, i.CorrectsInvoiceId)).ToList();

        var invoiceLines = (await invoiceFilter.OrderBy(i => i.IssueDate).ThenBy(i => i.Number)
            .SelectMany(i => i.Lines.Select(l => new
            {
                l.Id, InvoiceId = i.Id, InvoiceNumber = i.Number, l.SortOrder, l.BusinessLine, l.ItemKind, l.Description, l.Quantity, l.UnitPrice, l.LineDiscount,
                l.InvoiceDiscountShare, l.TaxClassCode, l.TaxRatePercent, l.NetAmount, l.TaxAmount, l.GrossAmount,
            })).Take(Cap).ToListAsync())
            .Select(l => new ExportInvoiceLine(l.Id, l.InvoiceId, l.InvoiceNumber!, l.SortOrder, l.BusinessLine.DisplayName, l.ItemKind.DisplayName, l.Description, l.Quantity,
                l.UnitPrice, l.LineDiscount, l.InvoiceDiscountShare, l.TaxClassCode, l.TaxRatePercent, l.NetAmount, l.TaxAmount, l.GrossAmount)).ToList();

        var payments = await PaymentsAsync(from, to, branchId);
        var allocations = await AllocationsAsync(from, to, branchId);

        var creditFilter = db.Set<CreditNoteAggregate>().AsNoTracking().Where(c => c.IssueDate >= from && c.IssueDate <= to && (branchId == null || c.BranchId == branchId));
        var invoiceNumbers = db.Set<InvoiceAggregate>().AsNoTracking();

        var creditNotes = (await (
            from c in creditFilter.OrderBy(c => c.IssueDate).ThenBy(c => c.Number).Take(Cap)
            join i in invoiceNumbers on c.InvoiceId equals i.Id
            select new { c.Id, c.Number, c.InvoiceId, InvoiceNumber = i.Number, c.BranchId, c.CustomerId, c.Kind, c.CurrencyCode, c.IssueDate, c.NetTotal, c.TaxTotal, c.Total, c.ReleasedToCustomerCredit })
            .ToListAsync())
            .Select(c => new ExportCreditNote(c.Id, c.Number, c.InvoiceId, c.InvoiceNumber ?? string.Empty, c.BranchId, c.CustomerId, c.Kind.DisplayName, c.CurrencyCode, c.IssueDate,
                c.NetTotal, c.TaxTotal, c.Total, c.ReleasedToCustomerCredit)).ToList();

        var creditNoteLines = (await creditFilter.OrderBy(c => c.IssueDate).ThenBy(c => c.Number)
            .SelectMany(c => c.Lines.Select(l => new
            {
                l.Id, CreditNoteId = c.Id, CreditNoteNumber = c.Number, l.SortOrder, l.InvoiceLineId, l.Description, l.TaxRatePercent, l.NetAmount, l.TaxAmount, l.GrossAmount,
            })).Take(Cap).ToListAsync())
            .Select(l => new ExportCreditNoteLine(l.Id, l.CreditNoteId, l.CreditNoteNumber, l.SortOrder, l.InvoiceLineId, l.Description, l.TaxRatePercent, l.NetAmount, l.TaxAmount, l.GrossAmount)).ToList();

        var refunds = (await db.Set<RefundAggregate>().AsNoTracking()
            .Where(r => r.BusinessDate >= from && r.BusinessDate <= to && (branchId == null || r.BranchId == branchId))
            .OrderBy(r => r.BusinessDate).ThenBy(r => r.Id).Take(Cap)
            .Select(r => new { r.Id, r.PaymentId, r.CreditNoteId, r.BranchId, r.CustomerId, r.Method, r.Amount, r.CurrencyCode, r.BusinessDate, r.Reference }).ToListAsync())
            .Select(r => new ExportRefund(r.Id, r.PaymentId, r.CreditNoteId, r.BranchId, r.CustomerId, r.Method.DisplayName, r.Amount, r.CurrencyCode, r.BusinessDate, r.Reference)).ToList();

        var currency = settings.GetString(SettingKeys.CommerceCurrencyCode);
        return new QueryResponse<AccountingExportContent>
        {
            Result = new AccountingExportContent(from, to, branchId, currency, invoices, invoiceLines, payments, allocations, creditNotes, creditNoteLines, refunds),
        };
    }

    private async Task<List<ExportPayment>> PaymentsAsync(DateOnly from, DateOnly to, Guid? branchId)
    {
        var accepted = (await db.Set<PaymentAggregate>().AsNoTracking()
            .Where(p => p.AcceptedOn != null && p.BusinessDate >= from && p.BusinessDate <= to && (branchId == null || p.BranchId == branchId))
            .OrderBy(p => p.BusinessDate).ThenBy(p => p.ReceiptNumber).Take(Cap)
            .Select(p => new { p.Id, p.ReceiptNumber, p.BranchId, p.CustomerId, p.Method, p.Amount, p.CurrencyCode, p.BusinessDate, p.ReceivedOn, p.ExternalReference, p.Collector })
            .ToListAsync())
            .Select(p => new ExportPayment("Payment", p.Id, p.ReceiptNumber, p.BranchId, p.CustomerId, p.Method.DisplayName, p.Amount, p.CurrencyCode, p.BusinessDate!.Value,
                p.ReceivedOn, p.ExternalReference, p.Collector?.Kind.DisplayName)).ToList();

        var reversals = (await db.Set<PaymentAggregate>().AsNoTracking()
            .Where(p => p.ReversalBusinessDate != null && p.ReversalBusinessDate >= from && p.ReversalBusinessDate <= to && (branchId == null || p.BranchId == branchId))
            .OrderBy(p => p.ReversalBusinessDate).ThenBy(p => p.ReceiptNumber).Take(Cap)
            .Select(p => new { p.Id, p.ReceiptNumber, p.BranchId, p.CustomerId, p.Method, p.Amount, p.CurrencyCode, p.ReversalBusinessDate, p.ReceivedOn, p.ExternalReference, p.Collector })
            .ToListAsync())
            .Select(p => new ExportPayment("PaymentReversal", p.Id, p.ReceiptNumber, p.BranchId, p.CustomerId, p.Method.DisplayName, p.Amount, p.CurrencyCode, p.ReversalBusinessDate!.Value,
                p.ReceivedOn, p.ExternalReference, p.Collector?.Kind.DisplayName)).ToList();

        return [.. accepted, .. reversals];
    }

    /// <summary>An allocation entry is dated by its local date at its payment's branch; the UTC window is widened a day each side, then filtered exactly.</summary>
    private async Task<List<ExportAllocation>> AllocationsAsync(DateOnly from, DateOnly to, Guid? branchId)
    {
        var windowStart = new DateTimeOffset(from.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(to.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var rows = await db.Set<PaymentAggregate>().AsNoTracking()
            .Where(p => branchId == null || p.BranchId == branchId)
            .SelectMany(p => p.Entries.Where(e => e.OccurredOn >= windowStart && e.OccurredOn < windowEnd)
                .Select(e => new { e.Id, PaymentId = p.Id, p.BranchId, e.InvoiceId, e.Kind, e.ReleaseReason, e.Amount, e.OccurredOn }))
            .Take(Cap * 3)
            .ToListAsync();

        var zones = (await db.Set<BranchAggregate>().AsNoTracking().Select(b => new { b.Id, b.TimeZoneId }).ToListAsync()).ToDictionary(b => b.Id, b => b.TimeZoneId);

        return rows
            .Select(r =>
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(zones.GetValueOrDefault(r.BranchId, BillingRules.DefaultTimeZoneId));
                var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(r.OccurredOn, zone).DateTime);
                return new ExportAllocation(r.Id, r.PaymentId, r.InvoiceId, r.Kind.DisplayName, r.ReleaseReason?.DisplayName, r.Amount, date, r.OccurredOn);
            })
            .Where(a => a.Date >= from && a.Date <= to)
            .ToList();
    }
}

public sealed class GetAccountingExportRunsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetAccountingExportRunsQuery, PagedResult<AccountingExportRunViewModel>>
{
    public async Task<QueryResponse<PagedResult<AccountingExportRunViewModel>>> HandleAsync(GetAccountingExportRunsQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);
        var pageNumber = Math.Max(query.PageNumber, 1);

        var runs = db.Set<AccountingExportRunAggregate>().AsNoTracking();
        var total = await runs.CountAsync();
        var rows = await (
            from r in runs.OrderByDescending(r => r.RunOn).ThenBy(r => r.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            join staff in db.Set<StaffAccountAggregate>().AsNoTracking() on r.RunBy equals staff.Id into staffRows
            from staff in staffRows.DefaultIfEmpty()
            select new AccountingExportRunViewModel(
                r.Id, r.From, r.To, r.BranchId, r.InvoiceCount, r.InvoiceLineCount, r.PaymentCount, r.AllocationCount, r.CreditNoteCount, r.RefundCount, r.Sha256, r.RunBy,
                staff != null ? staff.FullName : string.Empty, r.RunOn)).ToListAsync();

        return new QueryResponse<PagedResult<AccountingExportRunViewModel>> { Result = new PagedResult<AccountingExportRunViewModel>(rows, pageNumber, pageSize, total) };
    }
}
