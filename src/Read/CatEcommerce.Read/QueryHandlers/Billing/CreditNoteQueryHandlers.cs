using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Infrastructure.Billing.Documents;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Billing;

/// <summary>HO-BIL-001 section 12.4 -- credit notes issued against one invoice, oldest first.</summary>
internal static class InvoiceCreditNotesLookup
{
    public static async Task<IReadOnlyList<InvoiceCreditNoteViewModel>> ForInvoiceAsync(CatEcommerceDbContext db, Guid invoiceId, string currency)
    {
        var rows = await db.Set<CreditNoteAggregate>().AsNoTracking().Where(c => c.InvoiceId == invoiceId)
            .OrderBy(c => c.IssuedOn).ThenBy(c => c.Id)
            .Select(c => new { c.Id, c.Number, c.Kind, c.Total, c.IssueDate })
            .ToListAsync();

        return rows.Select(c => new InvoiceCreditNoteViewModel(c.Id, c.Number, c.Kind.DisplayName, BillingMoney.Of(c.Total, currency), c.IssueDate)).ToList();
    }
}

/// <summary>The refunds drawn from one payment, oldest first.</summary>
internal static class PaymentRefundsLookup
{
    public static async Task<IReadOnlyList<PaymentRefundViewModel>> ForPaymentAsync(CatEcommerceDbContext db, Guid paymentId, string currency)
    {
        var rows = await (
            from r in db.Set<RefundAggregate>().AsNoTracking().Where(r => r.PaymentId == paymentId)
            join c in db.Set<CreditNoteAggregate>().AsNoTracking() on r.CreditNoteId equals c.Id into notes
            from c in notes.DefaultIfEmpty()
            orderby r.IssuedOn, r.Id
            select new { r.Id, r.Method, r.Amount, r.BusinessDate, r.CreditNoteId, CreditNoteNumber = c != null ? c.Number : null }).ToListAsync();

        return rows.Select(r => new PaymentRefundViewModel(r.Id, r.Method.DisplayName, BillingMoney.Of(r.Amount, currency), r.BusinessDate, r.CreditNoteId, r.CreditNoteNumber)).ToList();
    }
}

public sealed class GetCreditNoteDetailQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCreditNoteDetailQuery, CreditNoteDetailViewModel?>
{
    public async Task<QueryResponse<CreditNoteDetailViewModel?>> HandleAsync(GetCreditNoteDetailQuery query)
    {
        var note = await db.Set<CreditNoteAggregate>().AsNoTracking().Where(c => c.Id == query.CreditNoteId && c.BranchId == query.BranchId)
            .Select(c => new
            {
                c.Id, c.Number, c.FinancialYear, c.Kind, c.Reason, c.InvoiceId, c.CustomerId, c.BranchId, c.CurrencyCode, c.NetTotal, c.TaxTotal, c.Total,
                c.ReleasedToCustomerCredit, c.IssuedOn, c.IssueDate, c.IssuedBy,
            }).FirstOrDefaultAsync();
        if (note is null) return new QueryResponse<CreditNoteDetailViewModel?> { Result = null };

        var currency = note.CurrencyCode;
        var invoiceNumber = await db.Set<InvoiceAggregate>().AsNoTracking().Where(i => i.Id == note.InvoiceId).Select(i => i.Number).FirstOrDefaultAsync();
        var issuer = await db.Set<StaffAccountAggregate>().AsNoTracking().Where(s => s.Id == note.IssuedBy).Select(s => s.FullName).FirstOrDefaultAsync() ?? string.Empty;
        var correctedBy = await db.Set<InvoiceAggregate>().AsNoTracking()
            .Where(i => i.CorrectsCreditNoteId == note.Id && i.Status != InvoiceStatus.Discarded)
            .Select(i => (Guid?)i.Id).FirstOrDefaultAsync();

        var lineRows = await db.Set<CreditNoteAggregate>().AsNoTracking().Where(c => c.Id == note.Id).SelectMany(c => c.Lines).OrderBy(l => l.SortOrder)
            .Select(l => new { l.InvoiceLineId, l.Description, l.Quantity, l.RequestedAmount, l.NetAmount, l.TaxRatePercent, l.TaxAmount, l.GrossAmount }).ToListAsync();

        var lines = lineRows.Select(l => new CreditNoteLineViewModel(
            l.InvoiceLineId, l.Description, l.Quantity is { } q ? BillingMoney.Quantity(q) : null,
            l.RequestedAmount is { } a ? BillingMoney.Of(a, currency) : null, BillingMoney.Of(l.NetAmount, currency),
            l.TaxRatePercent.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), BillingMoney.Of(l.TaxAmount, currency), BillingMoney.Of(l.GrossAmount, currency))).ToList();

        return new QueryResponse<CreditNoteDetailViewModel?>
        {
            Result = new CreditNoteDetailViewModel(
                note.Id, note.Number, note.FinancialYear, note.Kind.DisplayName, note.Reason, note.InvoiceId, invoiceNumber, note.CustomerId, note.BranchId, lines,
                BillingMoney.Of(note.NetTotal, currency), BillingMoney.Of(note.TaxTotal, currency), BillingMoney.Of(note.Total, currency),
                BillingMoney.Of(note.ReleasedToCustomerCredit, currency), note.IssuedOn, note.IssueDate, note.IssuedBy, issuer, correctedBy),
        };
    }
}

/// <summary>The credit-note PDF data: the seller and bill-to snapshots of the invoice it credits, and the credited lines at the frozen rate.</summary>
public sealed class GetCreditNoteDocumentQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCreditNoteDocumentQuery, CreditNoteDocument?>
{
    public async Task<QueryResponse<CreditNoteDocument?>> HandleAsync(GetCreditNoteDocumentQuery query)
    {
        var note = await db.Set<CreditNoteAggregate>().AsNoTracking().Where(c => c.Id == query.CreditNoteId && c.BranchId == query.BranchId)
            .Select(c => new
            {
                c.Id, c.Number, c.Kind, c.CurrencyCode, c.IssuedOn, c.BranchId, c.InvoiceId, c.Reason, c.NetTotal, c.TaxTotal, c.Total, c.ReleasedToCustomerCredit,
            }).FirstOrDefaultAsync();
        var invoice = note is null ? null : await db.Set<InvoiceAggregate>().AsNoTracking().Where(i => i.Id == note.InvoiceId)
            .Select(i => new { i.Number, i.Seller, i.BillTo }).FirstOrDefaultAsync();
        if (note is null || invoice?.Seller is null || invoice.BillTo is null)
            return new QueryResponse<CreditNoteDocument?> { Result = null };

        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == note.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var lineRows = await db.Set<CreditNoteAggregate>().AsNoTracking().Where(c => c.Id == note.Id).SelectMany(c => c.Lines).OrderBy(l => l.SortOrder)
            .Select(l => new { l.Description, l.Quantity, l.TaxRatePercent, l.NetAmount, l.TaxAmount, l.GrossAmount }).ToListAsync();
        var lines = lineRows.Select((l, index) => new DocumentLine(
            index + 1, string.Empty, string.Empty, l.Description, l.Quantity ?? 1m, 0m, 0m, 0m, null, l.TaxRatePercent, l.NetAmount, l.TaxAmount, l.GrossAmount)).ToList();

        var document = new CreditNoteDocument(
            note.Id, note.Number, note.Kind.DisplayName, note.CurrencyCode, note.IssuedOn, timeZoneId, invoice.Number ?? string.Empty,
            invoice.Seller.LegalName, invoice.Seller.TaxRegistrationNumber, invoice.Seller.AddressText.Replace("\n", ", "), invoice.Seller.Phone, invoice.Seller.Email,
            invoice.Seller.InvoiceFooter, invoice.BillTo.FullName, invoice.BillTo.Mobile, invoice.BillTo.Email, invoice.BillTo.AddressText, note.Reason,
            lines, note.NetTotal, note.TaxTotal, note.Total, note.ReleasedToCustomerCredit);

        return new QueryResponse<CreditNoteDocument?> { Result = document };
    }
}

public sealed class GetRefundsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetRefundsQuery, PagedResult<RefundListItemViewModel>>
{
    public async Task<QueryResponse<PagedResult<RefundListItemViewModel>>> HandleAsync(GetRefundsQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);
        var pageNumber = Math.Max(query.PageNumber, 1);

        var refunds = db.Set<RefundAggregate>().AsNoTracking().Where(r => r.BranchId == query.BranchId);
        if (query.From is { } rangeFrom) refunds = refunds.Where(r => r.BusinessDate >= rangeFrom);
        if (query.To is { } rangeTo) refunds = refunds.Where(r => r.BusinessDate <= rangeTo);

        var total = await refunds.CountAsync();
        var rows = await (
            from r in refunds.OrderByDescending(r => r.IssuedOn).ThenBy(r => r.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            join p in db.Set<PaymentAggregate>().AsNoTracking() on r.PaymentId equals p.Id
            join c in db.Set<CreditNoteAggregate>().AsNoTracking() on r.CreditNoteId equals c.Id into notes
            from c in notes.DefaultIfEmpty()
            join customer in db.Set<CustomerAccountAggregate>().AsNoTracking() on r.CustomerId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            join staff in db.Set<StaffAccountAggregate>().AsNoTracking() on r.IssuedBy equals staff.Id into staffRows
            from staff in staffRows.DefaultIfEmpty()
            select new
            {
                r.Id, r.PaymentId, p.ReceiptNumber, r.CreditNoteId, CreditNoteNumber = c != null ? c.Number : null, r.CustomerId,
                CustomerName = customer != null ? customer.FullName : "", r.Method, r.Amount, r.CurrencyCode, r.Reference, r.Reason, r.BusinessDate, r.IssuedOn,
                r.IssuedBy, IssuedByName = staff != null ? staff.FullName : "",
            }).ToListAsync();

        var items = rows.Select(r => new RefundListItemViewModel(
            r.Id, r.PaymentId, PaymentReadSupport.Receipt(r.ReceiptNumber), r.CreditNoteId, r.CreditNoteNumber, r.CustomerId, r.CustomerName, r.Method.DisplayName,
            BillingMoney.Of(r.Amount, r.CurrencyCode), r.Reference, r.Reason, r.BusinessDate, r.IssuedOn, r.IssuedBy, r.IssuedByName)).ToList();

        return new QueryResponse<PagedResult<RefundListItemViewModel>> { Result = new PagedResult<RefundListItemViewModel>(items, pageNumber, pageSize, total) };
    }
}
