using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Billing;

/// <summary>Small formatting helpers shared by the payment reads (a missing name prints as an empty string, never drops a row). The lookups
/// themselves are projections, in <see cref="PaymentProjections"/>.</summary>
internal static class PaymentReadSupport
{
    public static string? Receipt(long? number) => number is { } n ? BillingMessages.ReceiptNumber(n) : null;

    public static string Name(Dictionary<Guid, string> names, Guid? id) => id is { } key && names.TryGetValue(key, out var name) ? name : string.Empty;
}

public sealed class GetPaymentsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetPaymentsQuery, PagedResult<PaymentSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<PaymentSummaryViewModel>>> HandleAsync(GetPaymentsQuery query)
    {
        var pageSize = query.ForExport ? BillingRules.MaxExportRows : Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);
        var pageNumber = query.ForExport ? 1 : Math.Max(query.PageNumber, 1);

        var payments = db.Set<PaymentAggregate>().AsNoTracking().Where(p => !p.IsMarkedToDelete && p.BranchId == query.BranchId);
        if (query.CustomerId is { } customerId) payments = payments.Where(p => p.CustomerId == customerId);
        if (EnumerationParsing.TryParse<PaymentStatus>(query.Status, out var status) && status is not null) payments = payments.Where(p => p.Status == status);
        if (EnumerationParsing.TryParse<PaymentMethod>(query.Method, out var method) && method is not null) payments = payments.Where(p => p.Method == method);
        if (query.From is { } from) payments = payments.Where(p => p.ReceivedOn >= from);
        if (query.To is { } to) payments = payments.Where(p => p.ReceivedOn <= to);

        var total = await payments.CountAsync();
        var rows = await (
            from p in payments.OrderByDescending(p => p.RecordedOn).ThenBy(p => p.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            join customer in db.Set<CustomerAccountAggregate>().AsNoTracking() on p.CustomerId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            join staff in db.Set<StaffAccountAggregate>().AsNoTracking() on p.RecordedBy equals staff.Id into staffRows
            from staff in staffRows.DefaultIfEmpty()
            select new
            {
                p.Id, p.ReceiptNumber, p.CustomerId, CustomerName = customer != null ? customer.FullName : "", p.BranchId, p.Method, p.Status, p.Amount, p.CurrencyCode,
                Unallocated = p.Amount
                    - p.Entries.Where(e => e.Kind == AllocationEntryKind.Allocate).Sum(e => e.Amount)
                    + p.Entries.Where(e => e.Kind == AllocationEntryKind.Release).Sum(e => e.Amount)
                    - p.RefundedTotal,
                p.ReceivedOn, p.RecordedOn, p.RecordedBy, RecordedByName = staff != null ? staff.FullName : "", p.ExternalReference, p.Version,
            }).ToListAsync();

        var items = rows.Select(r => new PaymentSummaryViewModel(
            r.Id, PaymentReadSupport.Receipt(r.ReceiptNumber), r.CustomerId, r.CustomerName, r.BranchId, r.Method.DisplayName, r.Status.DisplayName,
            BillingMoney.Of(r.Amount, r.CurrencyCode), BillingMoney.Of(r.Status == PaymentStatus.Accepted ? r.Unallocated : 0m, r.CurrencyCode),
            r.ReceivedOn, r.RecordedOn, r.RecordedBy, r.RecordedByName, r.ExternalReference, r.Version)).ToList();

        return new QueryResponse<PagedResult<PaymentSummaryViewModel>> { Result = new PagedResult<PaymentSummaryViewModel>(items, pageNumber, pageSize, total) };
    }
}

public sealed class GetPaymentDetailQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetPaymentDetailQuery, PaymentDetailViewModel?>
{
    public async Task<QueryResponse<PaymentDetailViewModel?>> HandleAsync(GetPaymentDetailQuery query)
    {
        var payment = await PaymentProjections.HeaderAsync(db.Set<PaymentAggregate>().AsNoTracking().Where(p => p.Id == query.PaymentId && p.BranchId == query.BranchId));
        if (payment is null) return new QueryResponse<PaymentDetailViewModel?> { Result = null };

        var currency = payment.CurrencyCode;
        var entryRows = await PaymentProjections.EntriesAsync(db, payment.Id);
        var requested = await PaymentProjections.RequestedAsync(db, payment.Id);

        var customerName = (await PaymentProjections.CustomerNamesAsync(db, [payment.CustomerId])).GetValueOrDefault(payment.CustomerId, "");
        var staff = await PaymentProjections.StaffNamesAsync(db, [payment.RecordedBy, payment.ApprovedBy, payment.RejectedBy, payment.ReversedBy, payment.Collector?.StaffAccountId]);
        var numbers = await PaymentProjections.InvoiceNumbersAsync(db, entryRows.Select(e => e.InvoiceId).Concat(requested.Select(r => r.InvoiceId)));

        var unallocated = payment.Status == PaymentStatus.Accepted ? payment.Unallocated : 0m;
        var summary = new PaymentSummaryViewModel(
            payment.Id, PaymentReadSupport.Receipt(payment.ReceiptNumber), payment.CustomerId, customerName, payment.BranchId, payment.Method.DisplayName,
            payment.Status.DisplayName, BillingMoney.Of(payment.Amount, currency), BillingMoney.Of(unallocated, currency), payment.ReceivedOn, payment.RecordedOn,
            payment.RecordedBy, PaymentReadSupport.Name(staff, payment.RecordedBy), payment.ExternalReference, payment.Version);

        var collector = payment.Collector is null ? null : new PaymentCollectorViewModel(
            payment.Collector.Kind.DisplayName, payment.Collector.StaffAccountId, payment.Collector.StaffAccountId is { } sid ? PaymentReadSupport.Name(staff, sid) : null,
            payment.Collector.CourierCode, payment.Collector.CourierName);

        var earmark = payment.Earmark is null ? null : new PaymentEarmarkViewModel(
            payment.Earmark.Kind.DisplayName, payment.Earmark.SourceId, payment.Earmark.Label, payment.Earmark.IsReleased, payment.Earmark.ReleasedOn, payment.Earmark.ReleaseReason);

        var entries = entryRows.Select(e => new PaymentEntryViewModel(
            e.Id, e.Kind.DisplayName, e.InvoiceId, numbers.GetValueOrDefault(e.InvoiceId), BillingMoney.Of(e.Amount, currency), e.OccurredOn, e.By,
            e.ReleaseReason?.DisplayName)).ToList();

        var trail = new PaymentApprovalTrailViewModel(
            payment.RecordedBy, PaymentReadSupport.Name(staff, payment.RecordedBy), payment.RecordedOn,
            payment.ApprovedBy, payment.ApprovedBy is null ? null : PaymentReadSupport.Name(staff, payment.ApprovedBy), payment.ApprovedOn,
            payment.RejectedBy, payment.RejectedBy is null ? null : PaymentReadSupport.Name(staff, payment.RejectedBy), payment.RejectedOn, payment.RejectionReason,
            payment.ReversedBy, payment.ReversedBy is null ? null : PaymentReadSupport.Name(staff, payment.ReversedBy), payment.ReversedOn, payment.ReversalReason);

        var refunds = await PaymentRefundsLookup.ForPaymentAsync(db, payment.Id, currency);

        var result = new PaymentDetailViewModel(
            summary, payment.Notes, payment.AcceptedOn, payment.BusinessDate, collector, earmark, entries, refunds, trail, BillingMoney.Of(payment.RefundedTotal, currency),
            requested.Select(r => new RequestedAllocationViewModel(r.InvoiceId, numbers.GetValueOrDefault(r.InvoiceId), BillingMoney.Of(r.Amount, currency))).ToList(),
            PaymentActionRules.AllowedActions(payment.Status, unallocated, payment.RefundedTotal, payment.Earmark is { IsReleased: false }));

        return new QueryResponse<PaymentDetailViewModel?> { Result = result };
    }
}


/// <summary>HO-BIL-001 §12.4 -- print data for a receipt (staff or the customer's own).</summary>
public sealed class GetPaymentReceiptQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetPaymentReceiptQuery, PaymentReceiptViewModel?>
{
    public async Task<QueryResponse<PaymentReceiptViewModel?>> HandleAsync(GetPaymentReceiptQuery query)
    {
        var payments = db.Set<PaymentAggregate>().AsNoTracking().Where(p => p.Id == query.PaymentId);
        if (query.BranchId is { } branchId) payments = payments.Where(p => p.BranchId == branchId);
        if (query.CustomerId is { } customerId)
        {
            var customers = CustomerSet.Of(db, customerId);
            payments = payments.Where(p => customers.Contains(p.CustomerId) && (p.Status == PaymentStatus.Accepted || p.Status == PaymentStatus.Reversed));
        }

        var payment = await PaymentProjections.HeaderAsync(payments);
        if (payment is null) return new QueryResponse<PaymentReceiptViewModel?> { Result = null };

        var branch = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == payment.BranchId).Select(b => new { b.Name, b.TimeZoneId }).FirstOrDefaultAsync();
        var profile = await db.Set<BillingProfileAggregate>().AsNoTracking().Where(p => p.Id == payment.BranchId)
            .Select(p => new
            {
                p.LegalName, p.TaxRegistrationNumber, p.AddressLines, p.Phone, p.Email, p.InvoiceFooter,
                Accounts = p.BankAccounts.OrderBy(a => a.SortOrder).Select(a => new { a.BankName, a.AccountName, a.AccountNumber, a.BranchName, a.RoutingNumber }).ToList(),
            }).FirstOrDefaultAsync();
        var customerName = (await PaymentProjections.CustomerNamesAsync(db, [payment.CustomerId])).GetValueOrDefault(payment.CustomerId, "");
        var staff = await PaymentProjections.StaffNamesAsync(db, [payment.RecordedBy]);
        var effective = PaymentProjections.Effective(await PaymentProjections.EntriesAsync(db, payment.Id));
        var numbers = await PaymentProjections.InvoiceNumbersAsync(db, effective.Select(a => a.InvoiceId));

        var seller = profile is null ? null : new SellerViewModel(
            profile.LegalName, profile.TaxRegistrationNumber, string.Join("\n", profile.AddressLines), profile.Phone, profile.Email, profile.InvoiceFooter,
            profile.Accounts.Select(a => new BillingBankAccountViewModel(a.BankName, a.AccountName, a.AccountNumber, a.BranchName, a.RoutingNumber)).ToList());

        var appliedTo = effective.Select(a => numbers.GetValueOrDefault(a.InvoiceId) ?? string.Empty).Where(n => n.Length > 0).ToList();

        var result = new PaymentReceiptViewModel(
            PaymentReadSupport.Receipt(payment.ReceiptNumber), seller, branch?.Name ?? "", customerName, payment.Method.DisplayName, payment.ExternalReference,
            BillingMoney.Of(payment.Amount, payment.CurrencyCode), payment.ReceivedOn, payment.AcceptedOn, payment.Status.DisplayName, appliedTo,
            PaymentReadSupport.Name(staff, payment.RecordedBy), branch?.TimeZoneId ?? BillingRules.DefaultTimeZoneId);

        return new QueryResponse<PaymentReceiptViewModel?> { Result = result };
    }
}

public sealed class GetPendingApprovalsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetPendingApprovalsQuery, IReadOnlyList<PendingApprovalViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<PendingApprovalViewModel>>> HandleAsync(GetPendingApprovalsQuery query)
    {
        var payments = await db.Set<PaymentAggregate>().AsNoTracking()
            .Where(p => p.BranchId == query.BranchId && p.Status == PaymentStatus.PendingApproval)
            .OrderBy(p => p.RecordedOn).ThenBy(p => p.Id)
            .Take(BillingRules.MaxExportRows)
            .Select(p => new
            {
                p.Id, p.CustomerId, p.Method, p.Amount, p.CurrencyCode, p.ReceivedOn, p.RecordedOn, p.RecordedBy, p.ExternalReference,
                Requested = p.RequestedAllocations.Select(r => new { r.InvoiceId, r.Amount }).ToList(),
            })
            .ToListAsync();

        var customerNames = await PaymentProjections.CustomerNamesAsync(db, payments.Select(p => p.CustomerId));
        var staff = await PaymentProjections.StaffNamesAsync(db, payments.Select(p => (Guid?)p.RecordedBy));
        var numbers = await PaymentProjections.InvoiceNumbersAsync(db, payments.SelectMany(p => p.Requested.Select(r => r.InvoiceId)));

        IReadOnlyList<PendingApprovalViewModel> items = payments.Select(p => new PendingApprovalViewModel(
            p.Id, p.CustomerId, customerNames.GetValueOrDefault(p.CustomerId, ""), p.Method.DisplayName, BillingMoney.Of(p.Amount, p.CurrencyCode), p.ReceivedOn,
            p.RecordedOn, p.RecordedBy, PaymentReadSupport.Name(staff, p.RecordedBy), p.ExternalReference, CanApprove: p.RecordedBy != query.CallerId,
            p.Requested.Select(r => new RequestedAllocationViewModel(r.InvoiceId, numbers.GetValueOrDefault(r.InvoiceId), BillingMoney.Of(r.Amount, p.CurrencyCode))).ToList())).ToList();

        return new QueryResponse<IReadOnlyList<PendingApprovalViewModel>> { Result = items };
    }
}

public sealed class GetPaymentRecordResultQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetPaymentRecordResultQuery, PaymentRecordResultViewModel?>
{
    public async Task<QueryResponse<PaymentRecordResultViewModel?>> HandleAsync(GetPaymentRecordResultQuery query)
    {
        var payment = await PaymentProjections.HeaderAsync(db.Set<PaymentAggregate>().AsNoTracking().Where(p => p.Id == query.PaymentId));
        if (payment is null) return new QueryResponse<PaymentRecordResultViewModel?> { Result = null };

        var currency = payment.CurrencyCode;
        var effective = PaymentProjections.Effective(await PaymentProjections.EntriesAsync(db, payment.Id));
        var invoiceIds = effective.Select(a => a.InvoiceId).ToList();
        var invoices = await db.Set<InvoiceAggregate>().AsNoTracking().Where(i => invoiceIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Number, i.Total, i.PaidTotal, i.CreditedTotal, i.Status })
            .ToListAsync();

        var unallocated = payment.Status == PaymentStatus.Accepted ? payment.Unallocated : 0m;
        var result = new PaymentRecordResultViewModel(
            payment.Id, payment.Status.DisplayName, PaymentReadSupport.Receipt(payment.ReceiptNumber),
            effective.Select(a => new AppliedAllocationViewModel(a.InvoiceId, invoices.FirstOrDefault(i => i.Id == a.InvoiceId)?.Number, BillingMoney.Of(a.Amount, currency))).ToList(),
            BillingMoney.Of(unallocated, currency),
            invoices.Select(i => new PaymentInvoiceOutstandingViewModel(i.Id, i.Number, BillingMoney.Of(InvoiceStatusRules.Outstanding(i.Total, i.PaidTotal, i.CreditedTotal), currency), i.Status.DisplayName)).ToList());

        return new QueryResponse<PaymentRecordResultViewModel?> { Result = result };
    }
}

public sealed class GetCustomerPaymentsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCustomerPaymentsQuery, PagedResult<PaymentSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<PaymentSummaryViewModel>>> HandleAsync(GetCustomerPaymentsQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);
        var pageNumber = Math.Max(query.PageNumber, 1);
        var customers = CustomerSet.Of(db, query.CustomerId);

        var payments = db.Set<PaymentAggregate>().AsNoTracking()
            .Where(p => !p.IsMarkedToDelete && customers.Contains(p.CustomerId) && (p.Status == PaymentStatus.Accepted || p.Status == PaymentStatus.Reversed));

        var total = await payments.CountAsync();
        var rows = await payments.OrderByDescending(p => p.RecordedOn).ThenBy(p => p.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(p => new { p.Id, p.ReceiptNumber, p.CustomerId, p.BranchId, p.Method, p.Status, p.Amount, p.CurrencyCode, p.ReceivedOn, p.RecordedOn, p.ExternalReference, p.Version })
            .ToListAsync();

        var items = rows.Select(r => new PaymentSummaryViewModel(
            r.Id, PaymentReadSupport.Receipt(r.ReceiptNumber), r.CustomerId, "", r.BranchId, r.Method.DisplayName, r.Status.DisplayName, BillingMoney.Of(r.Amount, r.CurrencyCode),
            BillingMoney.Of(0m, r.CurrencyCode), r.ReceivedOn, r.RecordedOn, Guid.Empty, "", r.ExternalReference, r.Version)).ToList();

        return new QueryResponse<PagedResult<PaymentSummaryViewModel>> { Result = new PagedResult<PaymentSummaryViewModel>(items, pageNumber, pageSize, total) };
    }
}
