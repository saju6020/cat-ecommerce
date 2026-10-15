using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Billing.Documents;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Billing;

/// <summary>HO-BIL-001 §12.4 -- the staff invoice list. Explicit projection, paged in SQL; a discarded draft is not listed
/// unless asked for by status.</summary>
public sealed class GetInvoicesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetInvoicesQuery, PagedResult<InvoiceSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<InvoiceSummaryViewModel>>> HandleAsync(GetInvoicesQuery query)
    {
        var pageSize = query.ForExport ? BillingRules.MaxExportRows : Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);
        var pageNumber = query.ForExport ? 1 : Math.Max(query.PageNumber, 1);

        var invoices = db.Set<InvoiceAggregate>().AsNoTracking().Where(i => !i.IsMarkedToDelete && i.BranchId == query.BranchId);

        if (EnumerationParsing.TryParse<InvoiceStatus>(query.Status, out var status) && status is not null) invoices = invoices.Where(i => i.Status == status);
        else invoices = invoices.Where(i => i.Status != InvoiceStatus.Discarded);

        if (query.CustomerId is { } customerId) invoices = invoices.Where(i => i.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(query.Number)) invoices = invoices.Where(i => i.Number != null && EF.Functions.ILike(i.Number, "%" + EscapeLike(query.Number.Trim()) + "%"));
        if (query.From is { } from) invoices = invoices.Where(i => i.IssueDate >= from);
        if (query.To is { } to) invoices = invoices.Where(i => i.IssueDate <= to);
        if (EnumerationParsing.TryParse<BusinessLine>(query.BusinessLine, out var line) && line is not null)
            invoices = invoices.Where(i => i.Lines.Any(l => l.BusinessLine == line));

        var total = await invoices.CountAsync();
        var rows = await (
            from i in invoices.OrderByDescending(i => i.IssuedOn ?? i.CreatedDate).ThenBy(i => i.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            join customer in db.Set<CustomerAccountAggregate>().AsNoTracking() on i.CustomerId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            select new
            {
                i.Id, i.Number, i.Status, i.CustomerId, CustomerName = customer != null ? customer.FullName : "", i.BranchId, i.IssueDate, i.DueOn,
                i.Total, i.PaidTotal, i.CreditedTotal, i.CurrencyCode, i.Version,
            }).ToListAsync();

        var items = rows.Select(r => new InvoiceSummaryViewModel(
            r.Id, r.Number, r.Status.DisplayName, r.CustomerId, r.CustomerName, r.BranchId, r.IssueDate, r.DueOn,
            BillingMoney.Of(r.Total, r.CurrencyCode), BillingMoney.Of(r.PaidTotal, r.CurrencyCode), BillingMoney.Of(r.CreditedTotal, r.CurrencyCode),
            BillingMoney.Of(InvoiceStatusRules.Outstanding(r.Total, r.PaidTotal, r.CreditedTotal), r.CurrencyCode), r.Version)).ToList();

        return new QueryResponse<PagedResult<InvoiceSummaryViewModel>> { Result = new PagedResult<InvoiceSummaryViewModel>(items, pageNumber, pageSize, total) };
    }

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

/// <summary>HO-BIL-001 §12.4 -- the invoice detail (staff). <c>allowedActions</c> come from <see cref="InvoiceActionRules"/>,
/// the same table the aggregate's guards use, so a button the screen shows is never refused. A draft's totals are an
/// estimate at today's rates (<c>isEstimate</c>); an issued invoice's are the frozen ones.</summary>
public sealed class GetInvoiceDetailQueryHandler(CatEcommerceDbContext db, IBillingPricingInputs pricing, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetInvoiceDetailQuery, InvoiceDetailViewModel?>
{
    public async Task<QueryResponse<InvoiceDetailViewModel?>> HandleAsync(GetInvoiceDetailQuery query)
    {
        var header = await InvoiceProjections.HeaderAsync(db.Set<InvoiceAggregate>().AsNoTracking().Where(i => i.Id == query.InvoiceId && i.BranchId == query.BranchId));
        var result = header is null ? null : await InvoiceDetailBuilder.BuildAsync(db, pricing, timeProvider, header);
        return new QueryResponse<InvoiceDetailViewModel?> { Result = result };
    }
}

/// <summary>Shared by the staff and the customer detail: the one place an invoice becomes its view.</summary>
internal static class InvoiceDetailBuilder
{
    public static async Task<InvoiceDetailViewModel> BuildAsync(
        CatEcommerceDbContext db, IBillingPricingInputs pricing, TimeProvider timeProvider, InvoiceHeader invoice)
    {
        var currency = invoice.CurrencyCode;
        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == invoice.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var correctedBy = await db.Set<InvoiceAggregate>().AsNoTracking()
            .Where(i => i.CorrectsInvoiceId == invoice.Id && i.Status != InvoiceStatus.Discarded)
            .Select(i => (Guid?)i.Id).FirstOrDefaultAsync();

        var lines = await InvoiceProjections.LinesAsync(db, invoice.Id);
        var isDraft = invoice.Status == InvoiceStatus.Draft;

        IReadOnlyList<InvoiceLineViewModel> lineViews;
        InvoiceTotalsViewModel totals;
        if (isDraft)
        {
            var estimate = await InvoiceEstimator.EstimateAsync(
                pricing, currency,
                lines.Select(l => new InvoiceEstimator.Input(l.Id, l.BusinessLine.DisplayName, l.ItemKind.DisplayName, l.Description, l.Quantity, l.UnitPrice, l.LineDiscount, l.TaxClassId, l.ItemKind != ItemKind.Fee)).ToList(),
                invoice.LoyaltyValue ?? 0m, timeProvider.GetUtcNow());

            lineViews = lines.Select(l =>
            {
                var e = estimate.Lines.Single(x => x.Input.Key == l.Id);
                return new InvoiceLineViewModel(
                    l.Id, l.ChargeId, l.BusinessLine.DisplayName, l.ItemKind.DisplayName, l.SourceKind.DisplayName, l.SourceId, l.CatId, l.Description,
                    BillingMoney.Quantity(l.Quantity), BillingMoney.Of(l.UnitPrice, currency), BillingMoney.Of(l.LineDiscount, currency),
                    BillingMoney.Of(e.Result.InvoiceDiscountShare, currency), l.TaxClassId, e.TaxClassCode,
                    (e.RatePercent ?? 0m).ToString("F2", System.Globalization.CultureInfo.InvariantCulture), BillingMoney.Of(e.Result.NetAmount, currency),
                    BillingMoney.Of(e.Result.TaxAmount, currency), BillingMoney.Of(e.Result.GrossAmount, currency), l.SortOrder);
            }).ToList();
            totals = InvoiceEstimator.TotalsOf(estimate.Totals, currency);
        }
        else
        {
            lineViews = lines.Select(l => l.ToView(currency)).ToList();
            totals = new InvoiceTotalsViewModel(
                BillingMoney.Of(invoice.NetTotal, currency), BillingMoney.Of(invoice.DiscountTotal, currency),
                BillingMoney.Of(invoice.TaxTotal, currency), BillingMoney.Of(invoice.Total, currency));
        }

        var outstanding = isDraft ? 0m : invoice.Outstanding;
        var payments = await InvoicePaymentsLookup.ForInvoiceAsync(db, invoice.Id, currency);
        var creditNotes = await InvoiceCreditNotesLookup.ForInvoiceAsync(db, invoice.Id, currency);

        return new InvoiceDetailViewModel(
            invoice.Id, invoice.Number, invoice.FinancialYear, invoice.Status.DisplayName, invoice.Version, invoice.BranchId, timeZoneId, invoice.CustomerId,
            invoice.IssuedOn, invoice.IssueDate, invoice.DueOn, InvoiceViewMapper.SellerOf(invoice), InvoiceViewMapper.BillToOf(invoice),
            lineViews, totals, IsEstimate: isDraft,
            BillingMoney.Of(invoice.PaidTotal, currency), BillingMoney.Of(invoice.CreditedTotal, currency), BillingMoney.Of(outstanding, currency),
            invoice.LoyaltyPoints is { } points
                ? new LoyaltyRedemptionViewModel(points, BillingMoney.Of(invoice.LoyaltyValue ?? 0m, currency), invoice.LoyaltyEntryId ?? Guid.Empty)
                : null,
            invoice.Notes, invoice.CorrectsInvoiceId, invoice.CorrectsCreditNoteId, correctedBy, payments, creditNotes,
            InvoiceActionRules.AllowedActions(invoice.Status, outstanding));
    }
}

/// <summary>The payments applied to an invoice: each payment whose net allocation to it (Σ Allocate − Σ Release) is still positive.</summary>
internal static class InvoicePaymentsLookup
{
    public sealed record Row(Guid PaymentId, long? ReceiptNumber, PaymentMethod Method, PaymentStatus Status, decimal Applied, DateTimeOffset On, DateOnly ReceivedOn);

    public static async Task<IReadOnlyList<Row>> RowsAsync(CatEcommerceDbContext db, Guid invoiceId)
    {
        var rows = await db.Set<PaymentAggregate>().AsNoTracking()
            .Where(p => p.Entries.Any(e => e.InvoiceId == invoiceId))
            .Select(p => new
            {
                p.Id, p.ReceiptNumber, p.Method, p.Status, p.ReceivedOn,
                Applied = p.Entries.Where(e => e.InvoiceId == invoiceId && e.Kind == AllocationEntryKind.Allocate).Sum(e => e.Amount)
                          - p.Entries.Where(e => e.InvoiceId == invoiceId && e.Kind == AllocationEntryKind.Release).Sum(e => e.Amount),
                On = p.Entries.Where(e => e.InvoiceId == invoiceId).Max(e => e.OccurredOn),
            })
            .ToListAsync();

        return rows.Where(r => r.Applied > 0m).OrderBy(r => r.On).ThenBy(r => r.ReceiptNumber).ThenBy(r => r.Id).Select(r => new Row(r.Id, r.ReceiptNumber, r.Method, r.Status, r.Applied, r.On, r.ReceivedOn)).ToList();
    }

    public static async Task<IReadOnlyList<InvoicePaymentViewModel>> ForInvoiceAsync(CatEcommerceDbContext db, Guid invoiceId, string currency) =>
        (await RowsAsync(db, invoiceId))
            .Select(r => new InvoicePaymentViewModel(r.PaymentId, PaymentReadSupport.Receipt(r.ReceiptNumber) ?? string.Empty, r.Method.DisplayName, BillingMoney.Of(r.Applied, currency), r.On, r.Status.DisplayName))
            .ToList();
}


/// <summary>HO-BIL-001 §12.4 -- "preview": lines and totals at <b>today's</b> rates, <c>isEstimate: true</c>, nothing written.</summary>
public sealed class PreviewInvoiceQueryHandler(CatEcommerceDbContext db, IBillingPricingInputs pricing, IBusinessSettings settings, TimeProvider timeProvider)
    : IQueryHandlerAsync<PreviewInvoiceQuery, InvoicePreviewViewModel>
{
    public async Task<QueryResponse<InvoicePreviewViewModel>> HandleAsync(PreviewInvoiceQuery query)
    {
        var currency = settings.GetString(SettingKeys.CommerceCurrencyCode);
        var ids = query.ChargeIds.Distinct().ToList();

        var charges = await db.Charges.AsNoTracking()
            .Where(c => ids.Contains(c.Id) && c.CustomerId == query.CustomerId && c.BranchId == query.BranchId && c.Status == ChargeStatus.Active)
            .Select(c => new { c.Id, c.BusinessLine, c.ItemKind, c.Description, c.Quantity, c.UnitPriceAmount, c.LineDiscountAmount, c.TaxClassId })
            .ToListAsync();

        var problems = new List<string>();
        if (charges.Count != ids.Count) problems.Add(BusinessViolationCodes.ChargeNotFound);

        var loyaltyValue = 0m;
        if (query.LoyaltyPoints is > 0)
        {
            if (!settings.GetBool(SettingKeys.BillingLoyaltyEnabled)) problems.Add(BusinessViolationCodes.LoyaltyNotEnabled);
            else loyaltyValue = query.LoyaltyPoints.Value * settings.GetDecimal(SettingKeys.BillingLoyaltyPointValue);
        }

        var estimate = await InvoiceEstimator.EstimateAsync(
            pricing, currency,
            charges.Select(c => new InvoiceEstimator.Input(c.Id, c.BusinessLine.DisplayName, c.ItemKind.DisplayName, c.Description, c.Quantity, c.UnitPriceAmount, c.LineDiscountAmount, c.TaxClassId, c.ItemKind != ItemKind.Fee)).ToList(),
            loyaltyValue, timeProvider.GetUtcNow());

        problems.AddRange(estimate.Problems);

        var lines = estimate.Lines.Select(e => new InvoicePreviewLineViewModel(
            e.Input.Key, e.Input.BusinessLine, e.Input.ItemKind, e.Input.Description, BillingMoney.Quantity(e.Input.Quantity),
            BillingMoney.Of(e.Input.UnitPrice, currency), BillingMoney.Of(e.Input.LineDiscount, currency), BillingMoney.Of(e.Result.InvoiceDiscountShare, currency),
            e.TaxClassCode, e.RatePercent?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), BillingMoney.Of(e.Result.NetAmount, currency),
            BillingMoney.Of(e.Result.TaxAmount, currency), BillingMoney.Of(e.Result.GrossAmount, currency))).ToList();

        return new QueryResponse<InvoicePreviewViewModel>
        {
            Result = new InvoicePreviewViewModel(query.CustomerId, lines, InvoiceEstimator.TotalsOf(estimate.Totals, currency), IsEstimate: true, problems.Distinct().ToList()),
        };
    }
}

/// <summary>HO-BIL-001 §12.4 -- the data of an invoice PDF. Staff: branch-scoped; a customer: issued invoices of the caller
/// (or a customer merged into them) only. Draft and discarded invoices have no document.</summary>
public sealed class GetInvoiceDocumentQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetInvoiceDocumentQuery, InvoiceDocument?>
{
    public async Task<QueryResponse<InvoiceDocument?>> HandleAsync(GetInvoiceDocumentQuery query)
    {
        var invoices = VisibleToCustomer.Invoices(db.Set<InvoiceAggregate>().AsNoTracking()).Where(i => i.Id == query.InvoiceId);
        if (query.BranchId is { } branchId) invoices = invoices.Where(i => i.BranchId == branchId);
        if (query.CustomerId is { } customerId)
        {
            var customers = CustomerSet.Of(db, customerId);
            invoices = invoices.Where(i => customers.Contains(i.CustomerId));
        }

        var invoice = await InvoiceProjections.HeaderAsync(invoices);
        if (invoice is null || invoice.Seller is null || invoice.BillTo is null || invoice.Number is null || invoice.IssuedOn is null)
            return new QueryResponse<InvoiceDocument?> { Result = null };

        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == invoice.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var corrects = invoice.CorrectsInvoiceId is { } correctsId
            ? await db.Set<InvoiceAggregate>().AsNoTracking().Where(i => i.Id == correctsId).Select(i => i.Number).FirstOrDefaultAsync() : null;
        var payments = await InvoiceDocumentPayments.ForInvoiceAsync(db, invoice.Id);

        var lines = (await InvoiceProjections.LinesAsync(db, invoice.Id)).Select((l, index) => new DocumentLine(
            index + 1, l.BusinessLine.DisplayName, l.ItemKind.DisplayName, l.Description, l.Quantity, l.UnitPrice, l.LineDiscount, l.InvoiceDiscountShare,
            l.TaxClassCode, l.TaxRatePercent, l.NetAmount, l.TaxAmount, l.GrossAmount)).ToList();

        var document = new InvoiceDocument(
            invoice.Id, invoice.Number, invoice.CurrencyCode, invoice.IssuedOn.Value, timeZoneId, invoice.DueOn,
            invoice.Seller.LegalName, invoice.Seller.TaxRegistrationNumber, invoice.Seller.AddressText.Replace("\n", ", "), invoice.Seller.Phone, invoice.Seller.Email, invoice.Seller.InvoiceFooter,
            InvoiceViewMapper.BankAccounts(invoice.Seller.BankAccountsJson).Select(a => new DocumentBankAccount(a.BankName, a.AccountName, a.AccountNumber, a.BranchName, a.RoutingNumber)).ToList(),
            invoice.BillTo.FullName, invoice.BillTo.Mobile, invoice.BillTo.Email, invoice.BillTo.AddressText,
            lines, invoice.NetTotal, invoice.DiscountTotal, invoice.TaxTotal, invoice.Total, invoice.PaidTotal, invoice.CreditedTotal,
            invoice.LoyaltyPoints, invoice.LoyaltyValue, payments, corrects, null);

        return new QueryResponse<InvoiceDocument?> { Result = document };
    }
}

/// <summary>The payments printed on an invoice PDF: receipt, method, amount applied and the date received -- never a reference.</summary>
internal static class InvoiceDocumentPayments
{
    public static async Task<IReadOnlyList<DocumentPayment>> ForInvoiceAsync(CatEcommerceDbContext db, Guid invoiceId) =>
        (await InvoicePaymentsLookup.RowsAsync(db, invoiceId))
            .Select(r => new DocumentPayment(PaymentReadSupport.Receipt(r.ReceiptNumber) ?? string.Empty, r.Method.DisplayName, r.Applied, r.ReceivedOn))
            .ToList();
}

public sealed class GetCustomerInvoicesQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetCustomerInvoicesQuery, PagedResult<InvoiceSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<InvoiceSummaryViewModel>>> HandleAsync(GetCustomerInvoicesQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);
        var pageNumber = Math.Max(query.PageNumber, 1);
        var customers = CustomerSet.Of(db, query.CustomerId);

        var invoices = VisibleToCustomer.Invoices(db.Set<InvoiceAggregate>().AsNoTracking()).Where(i => !i.IsMarkedToDelete && customers.Contains(i.CustomerId));

        var total = await invoices.CountAsync();
        var rows = await invoices.OrderByDescending(i => i.IssuedOn).ThenBy(i => i.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(i => new { i.Id, i.Number, i.Status, i.CustomerId, i.BranchId, i.IssueDate, i.DueOn, i.Total, i.PaidTotal, i.CreditedTotal, i.CurrencyCode, i.Version })
            .ToListAsync();

        var items = rows.Select(r => new InvoiceSummaryViewModel(
            r.Id, r.Number, r.Status.DisplayName, r.CustomerId, "", r.BranchId, r.IssueDate, r.DueOn,
            BillingMoney.Of(r.Total, r.CurrencyCode), BillingMoney.Of(r.PaidTotal, r.CurrencyCode), BillingMoney.Of(r.CreditedTotal, r.CurrencyCode),
            BillingMoney.Of(InvoiceStatusRules.Outstanding(r.Total, r.PaidTotal, r.CreditedTotal), r.CurrencyCode), r.Version)).ToList();

        return new QueryResponse<PagedResult<InvoiceSummaryViewModel>> { Result = new PagedResult<InvoiceSummaryViewModel>(items, pageNumber, pageSize, total) };
    }
}

public sealed class GetCustomerInvoiceDetailQueryHandler(CatEcommerceDbContext db, IBillingPricingInputs pricing, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetCustomerInvoiceDetailQuery, CustomerInvoiceDetailViewModel?>
{
    public async Task<QueryResponse<CustomerInvoiceDetailViewModel?>> HandleAsync(GetCustomerInvoiceDetailQuery query)
    {
        var customers = CustomerSet.Of(db, query.CustomerId);
        var invoice = await InvoiceProjections.HeaderAsync(VisibleToCustomer.Invoices(db.Set<InvoiceAggregate>().AsNoTracking())
            .Where(i => i.Id == query.InvoiceId && customers.Contains(i.CustomerId)));

        if (invoice is null) return new QueryResponse<CustomerInvoiceDetailViewModel?> { Result = null };

        var detail = await InvoiceDetailBuilder.BuildAsync(db, pricing, timeProvider, invoice);
        var howToPay = new HowToPayViewModel(
            detail.Seller?.BankAccounts ?? [], $"Use your invoice number{(invoice.Number is null ? "" : " " + invoice.Number)} as the payment reference.");

        return new QueryResponse<CustomerInvoiceDetailViewModel?> { Result = new CustomerInvoiceDetailViewModel(detail, howToPay) };
    }
}
