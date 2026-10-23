using System.Globalization;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Read.QueryHandlers.Product;
using CatEcommerce.Read.QueryHandlers.StoredFile;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Order;

/// <summary>Paging as the other list endpoints do it: page size 1..100 (default 20), page number from 1.</summary>
internal static class OrderPaging
{
    public static (int PageNumber, int PageSize) Of(int pageNumber, int pageSize) => (Math.Max(pageNumber, 1), Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100));
}

/// <summary>The single source of the customer-facing re-order rule: list item, order detail and (through the list) the portal all ask here.</summary>
public static class CustomerOrderRules
{
    public static bool CanReorder(OrderStatus status) =>
        status == OrderStatus.Cancelled || status == OrderStatus.Delivered || status == OrderStatus.Returned || status == OrderStatus.Refunded;
}

/// <summary>HO-ORD-001 §12.4 -- the customer's order list. By caller only; a projection, never an aggregate.</summary>
public sealed class GetCustomerOrdersQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetCustomerOrdersQuery, PagedResult<CustomerOrderSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<CustomerOrderSummaryViewModel>>> HandleAsync(GetCustomerOrdersQuery query)
    {
        var (pageNumber, pageSize) = OrderPaging.Of(query.PageNumber, query.PageSize);
        var orders = db.Orders.AsNoTracking().Where(o => o.CustomerId == query.CustomerId);
        if (query.Status is { } status) orders = orders.Where(o => o.Status == status);

        var total = await orders.CountAsync();
        var rows = await orders.OrderByDescending(o => o.PlacedOn).ThenBy(o => o.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.PlacedOn, o.Status, o.FulfilmentMethod, o.PaymentMethod, o.CurrencyCode, GrandTotal = o.Totals.GrandTotal,
                ItemCount = o.Lines.Sum(l => l.Quantity),
                TrackingNumber = o.Shipment == null ? null : o.Shipment.TrackingNumber,
                TimeZoneId = db.Set<BranchAggregate>().Where(b => b.Id == o.BranchId).Select(b => b.TimeZoneId).FirstOrDefault(),
            })
            .ToListAsync();

        var items = rows.Select(r => new CustomerOrderSummaryViewModel(
            r.Id, r.OrderNumber, r.PlacedOn, r.Status.DisplayName, r.FulfilmentMethod.DisplayName, r.PaymentMethod.DisplayName,
            OrderMoney.Of(r.GrandTotal, r.CurrencyCode), r.ItemCount, r.TimeZoneId ?? "UTC",
            string.IsNullOrWhiteSpace(r.TrackingNumber) ? null : r.TrackingNumber, CustomerOrderRules.CanReorder(r.Status))).ToList();

        return new QueryResponse<PagedResult<CustomerOrderSummaryViewModel>> { Result = new PagedResult<CustomerOrderSummaryViewModel>(items, pageNumber, pageSize, total) };
    }
}

/// <summary>
/// HO-ORD-001 §12.4 -- one order of the caller's. <c>canCancel</c> and <c>canReorder</c> come from the same <see cref="OrderTransitionRules"/> the commands
/// enforce; <c>transferInstructions</c> (the shop's account, the reference, the amount) are shown only while a transfer order awaits payment; the status
/// history shows when, never who. A null result is <c>404</c>.
/// </summary>
public sealed class GetCustomerOrderDetailQueryHandler(CatEcommerceDbContext db, IBusinessSettings settings, TimeProvider timeProvider, IStoredFileSigningBatcher fileSigner)
    : IQueryHandlerAsync<GetCustomerOrderDetailQuery, CustomerOrderDetailViewModel?>
{
    public async Task<QueryResponse<CustomerOrderDetailViewModel?>> HandleAsync(GetCustomerOrderDetailQuery query)
    {
        var head = await db.Orders.AsNoTracking().Where(o => o.Id == query.OrderId && o.CustomerId == query.CustomerId)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.PlacedOn, o.Status, o.FulfilmentMethod, o.PaymentMethod, o.CurrencyCode, o.BranchId, o.PaymentDueBy, o.CustomerTransferReference,
                o.CollectByDate, o.InvoiceNumber, o.AmountPaid, o.AmountRefunded, o.RefundDue, o.DeliveredOn,
                Totals = new { o.Totals.Subtotal, o.Totals.LineDiscountTotal, o.Totals.OrderDiscount, o.Totals.DeliveryCharge, o.Totals.TaxTotal, o.Totals.GrandTotal },
                F = new
                {
                    o.Fulfilment.RecipientName, o.Fulfilment.RecipientPhone, o.Fulfilment.Line1, o.Fulfilment.Line2, o.Fulfilment.City, o.Fulfilment.District, o.Fulfilment.PostalCode,
                    o.Fulfilment.DeliveryInstructions, o.Fulfilment.ZoneName, o.Fulfilment.ServiceLevel, o.Fulfilment.SlotDate, o.Fulfilment.SlotWindowLabel, o.Fulfilment.PickupBranchId,
                },
                Ship = o.Shipment == null ? null : new { o.Shipment.CourierName, o.Shipment.TrackingNumber, o.Shipment.TrackingUrl, o.Shipment.LastTrackingStatus },
                Lines = o.Lines.OrderBy(l => l.LineNumber).Select(l => new
                {
                    l.Id, l.LineNumber, l.SkuId, l.ProductName, l.VariantLabel, l.CatId, l.Quantity, l.UnitPrice, l.LineSubtotal, l.LineDiscount, l.OrderDiscountShare, l.TaxRatePercent,
                    l.TaxAmount, l.LineTotal, l.QuantityReturned, l.ReturnClass,
                }).ToList(),
                History = o.StatusHistory.OrderBy(h => h.ChangedOn).Select(h => new { h.To, h.ChangedOn }).ToList(),
            })
            .FirstOrDefaultAsync();

        if (head is null) return new QueryResponse<CustomerOrderDetailViewModel?> { Result = null };

        var branch = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == head.BranchId).Select(b => new { b.TimeZoneId }).FirstOrDefaultAsync();
        var timeZoneId = branch?.TimeZoneId ?? "UTC";
        string currency = head.CurrencyCode;
        MoneyViewModel Money(decimal amount) => OrderMoney.Of(amount, currency);

        var catIds = head.Lines.Where(l => l.CatId != null).Select(l => l.CatId!.Value).Distinct().ToList();
        var cats = catIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<CatAggregate>().AsNoTracking().Where(c => catIds.Contains(c.Id) && c.OwnerId == query.CustomerId).Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name);

        string? pickupName = null;
        string? pickupAddress = null;
        if (head.F.PickupBranchId is { } pickupId)
        {
            var pickup = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == pickupId).Select(b => new { b.Name, b.AddressLine }).FirstOrDefaultAsync();
            pickupName = pickup?.Name;
            pickupAddress = pickup?.AddressLine;
        }

        OrderTransferInstructionsViewModel? transfer = null;
        if (head.Status == OrderStatus.PendingPayment && head.PaymentMethod == OrderPaymentMethod.BankTransfer)
        {
            var account = await db.BankTransferAccounts.AsNoTracking().Where(a => a.Id == BankTransferAccountAggregate.SingletonId)
                .Select(a => new { a.AccountName, a.AccountNumber, a.BankName, a.BranchName, a.RoutingNumber }).FirstOrDefaultAsync();
            if (account is not null)
                transfer = new OrderTransferInstructionsViewModel(account.AccountName, account.AccountNumber, account.BankName, account.BranchName, account.RoutingNumber, head.OrderNumber, Money(head.Totals.GrandTotal));
        }

        var canCancel = head.Status == OrderStatus.PendingPayment || head.Status == OrderStatus.Confirmed;
        var canReorder = CustomerOrderRules.CanReorder(head.Status);

        // Returns (US-ORD-13): only a delivered order, inside the window, with something left to send back.
        var windowDays = settings.GetInt(SettingKeys.OrdersReturnWindowDays);
        DateOnly? windowEnds = null;
        var returnable = new List<ReturnableLineViewModel>();
        var returnRows = await db.ReturnRequests.AsNoTracking().Where(r => r.OrderId == head.Id).OrderBy(r => r.RequestedOn)
            .Select(r => new { r.Id, r.Status, Open = r.Status == ReturnStatus.Requested || r.Status == ReturnStatus.Approved, Lines = r.Lines.Select(l => new { l.OrderLineId, l.Quantity }).ToList() })
            .ToListAsync();
        var openByLine = returnRows.Where(r => r.Open).SelectMany(r => r.Lines).GroupBy(l => l.OrderLineId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        if (head.Status == OrderStatus.Delivered && head.DeliveredOn is { } deliveredOn)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            windowEnds = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(deliveredOn, zone).DateTime).AddDays(windowDays);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).DateTime);
            if (today <= windowEnds)
                returnable = head.Lines
                    .Select(l => new { Line = l, Left = l.Quantity - l.QuantityReturned - openByLine.GetValueOrDefault(l.Id) })
                    .Where(x => x.Left > 0)
                    .Select(x => new ReturnableLineViewModel(x.Line.Id, x.Left, x.Line.ReturnClass.DisplayName)).ToList();
        }

        var refundRows = await db.OrderRefunds.AsNoTracking().Where(r => r.OrderId == head.Id).OrderBy(r => r.IssuedOn).Select(r => new { r.Method, r.Amount, r.IssuedOn }).ToListAsync();

        // The product's current photo, for the customer's own order screens (2026-10-16); staff views carry none.
        var imageBySku = await ProductImageUrls.ForSkusAsync(db, fileSigner, head.Lines.Select(l => l.SkuId).Distinct().ToList());

        var view = new CustomerOrderDetailViewModel(
            head.Id, head.OrderNumber, head.PlacedOn, head.Status.DisplayName, head.FulfilmentMethod.DisplayName, head.PaymentMethod.DisplayName, timeZoneId,
            head.Lines.Select(l => new OrderLineViewModel(
                l.Id, l.LineNumber, l.SkuId, l.ProductName, l.VariantLabel, l.CatId is { } c && cats.TryGetValue(c, out var catName) ? new CartCatViewModel(c, catName) : null,
                l.Quantity, Money(l.UnitPrice), Money(l.LineSubtotal), Money(l.LineDiscount), Money(l.OrderDiscountShare), l.TaxRatePercent, Money(l.TaxAmount), Money(l.LineTotal),
                imageBySku.GetValueOrDefault(l.SkuId))).ToList(),
            new OrderTotalsViewModel(
                Money(head.Totals.Subtotal), Money(head.Totals.LineDiscountTotal), Money(head.Totals.OrderDiscount), Money(head.Totals.DeliveryCharge), Money(head.Totals.TaxTotal), Money(head.Totals.GrandTotal)),
            new OrderFulfilmentViewModel(
                head.F.RecipientName, head.F.RecipientPhone, head.F.Line1, head.F.Line2, head.F.City, head.F.District, head.F.PostalCode, head.F.DeliveryInstructions, head.F.ZoneName,
                head.F.ServiceLevel?.DisplayName, head.F.SlotDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), head.F.SlotWindowLabel, head.F.PickupBranchId, pickupName, pickupAddress),
            head.History.Select(h => new CustomerStatusChangeViewModel(h.To.DisplayName, h.ChangedOn)).ToList(),
            head.Status == OrderStatus.PendingPayment ? head.PaymentDueBy : null, transfer, head.CustomerTransferReference,
            head.Ship is null ? null : new CustomerShipmentViewModel(head.Ship.CourierName, head.Ship.TrackingNumber, head.Ship.TrackingUrl, head.Ship.LastTrackingStatus),
            head.CollectByDate, head.InvoiceNumber, Money(head.AmountPaid), Money(head.AmountRefunded), Money(head.RefundDue),
            refundRows.Select(r => new CustomerRefundViewModel(r.Method.DisplayName, Money(r.Amount), r.IssuedOn)).ToList(),
            returnRows.Select(r => new CustomerReturnSummaryViewModel(r.Id, r.Status.DisplayName)).ToList(),
            canCancel, returnable.Count > 0, windowEnds, returnable, canReorder);

        return new QueryResponse<CustomerOrderDetailViewModel?> { Result = view };
    }
}

public sealed class GetAwaitingPaymentOrdersQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetAwaitingPaymentOrdersQuery, IReadOnlyList<AwaitingPaymentOrderViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<AwaitingPaymentOrderViewModel>>> HandleAsync(GetAwaitingPaymentOrdersQuery query)
    {
        var rows = await db.Orders.AsNoTracking()
            .Where(o => o.BranchId == query.BranchId && o.Status == OrderStatus.PendingPayment && o.PaymentMethod == OrderPaymentMethod.BankTransfer)
            .OrderBy(o => o.PaymentDueBy).ThenBy(o => o.Id)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.CurrencyCode, o.Totals.GrandTotal, o.PaymentDueBy, o.CustomerTransferReference, o.TransferReportedOn, o.Version,
                CustomerName = o.Fulfilment.RecipientName,
                Held = o.HeldPayment == null ? null : new { o.HeldPayment.Amount, o.HeldPayment.RecordedBy, o.HeldPayment.RecordedOn },
                TimeZoneId = db.Set<BranchAggregate>().Where(b => b.Id == o.BranchId).Select(b => b.TimeZoneId).FirstOrDefault(),
            })
            .Take(500)
            .ToListAsync();

        IReadOnlyList<AwaitingPaymentOrderViewModel> items = rows.Select(r => new AwaitingPaymentOrderViewModel(
            r.Id, r.OrderNumber, r.CustomerName, OrderMoney.Of(r.GrandTotal, r.CurrencyCode), r.PaymentDueBy, r.CustomerTransferReference, r.TransferReportedOn,
            r.Held is null ? null : new HeldPaymentViewModel(OrderMoney.Of(r.Held.Amount, r.CurrencyCode), r.Held.RecordedBy, r.Held.RecordedOn), r.Version, r.TimeZoneId ?? "UTC")).ToList();

        return new QueryResponse<IReadOnlyList<AwaitingPaymentOrderViewModel>> { Result = items };
    }
}

public sealed class GetReconciliationIssuesQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetReconciliationIssuesQuery, PagedResult<ReconciliationIssueViewModel>>
{
    public async Task<QueryResponse<PagedResult<ReconciliationIssueViewModel>>> HandleAsync(GetReconciliationIssuesQuery query)
    {
        var (pageNumber, pageSize) = OrderPaging.Of(query.PageNumber, query.PageSize);
        var issues = db.ReconciliationIssues.AsNoTracking().Where(i => i.BranchId == query.BranchId);
        if (query.Status is { } status) issues = issues.Where(i => i.Status == status);

        var total = await issues.CountAsync();
        var rows = await issues.OrderByDescending(i => i.RaisedOn).ThenBy(i => i.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(i => new
            {
                i.Id, i.OrderId, i.Kind, i.PaymentMethod, i.Amount, i.CurrencyCode, i.PaymentReference, i.PaymentReceivedOn, i.ErrorSummary, i.RaisedBy, i.RaisedOn, i.Status,
                i.ResolvedBy, i.ResolvedOn, i.ResolutionNote,
            })
            .ToListAsync();

        var items = rows.Select(i => new ReconciliationIssueViewModel(
            i.Id, i.OrderId, i.Kind.DisplayName, i.PaymentMethod, OrderMoney.Of(i.Amount, i.CurrencyCode), i.PaymentReference, i.PaymentReceivedOn, i.ErrorSummary, i.RaisedBy,
            i.RaisedOn, i.Status.DisplayName, i.ResolvedBy, i.ResolvedOn, i.ResolutionNote)).ToList();

        return new QueryResponse<PagedResult<ReconciliationIssueViewModel>> { Result = new PagedResult<ReconciliationIssueViewModel>(items, pageNumber, pageSize, total) };
    }
}

public sealed class GetRefundsDueQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetRefundsDueQuery, PagedResult<RefundDueOrderViewModel>>
{
    public async Task<QueryResponse<PagedResult<RefundDueOrderViewModel>>> HandleAsync(GetRefundsDueQuery query)
    {
        var (pageNumber, pageSize) = OrderPaging.Of(query.PageNumber, query.PageSize);
        // Money owed back: a cancelled or failed paid order (RefundDue > 0), or a received return that no refund has been issued for yet.
        var due = db.Orders.AsNoTracking().Where(o => o.BranchId == query.BranchId && (o.RefundDue > 0m
            || (o.AmountPaid > o.AmountRefunded && db.ReturnRequests.Any(r => r.OrderId == o.Id && (r.Status == ReturnStatus.Received || r.Status == ReturnStatus.Closed)
                && !db.OrderRefunds.Any(f => f.ReturnRequestId == r.Id)))));

        var total = await due.CountAsync();
        var rows = await due.OrderBy(o => o.CancelledOn).ThenBy(o => o.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                o.Id, o.OrderNumber, CustomerName = o.Fulfilment.RecipientName, o.Status, o.CurrencyCode, o.AmountPaid, o.AmountRefunded, o.RefundDue, o.CancelledOn,
                ReturnReceived = db.ReturnRequests.Any(r => r.OrderId == o.Id && (r.Status == ReturnStatus.Received || r.Status == ReturnStatus.Closed) && !db.OrderRefunds.Any(f => f.ReturnRequestId == r.Id)),
                TimeZoneId = db.Set<BranchAggregate>().Where(b => b.Id == o.BranchId).Select(b => b.TimeZoneId).FirstOrDefault(),
            })
            .ToListAsync();

        var items = rows.Select(r => new RefundDueOrderViewModel(
            r.Id, r.OrderNumber, r.CustomerName, r.Status.DisplayName, OrderMoney.Of(r.AmountPaid, r.CurrencyCode), OrderMoney.Of(r.AmountRefunded, r.CurrencyCode),
            OrderMoney.Of(r.RefundDue, r.CurrencyCode), r.CancelledOn, r.TimeZoneId ?? "UTC",
            new[] { r.RefundDue > 0m ? "MoneyOwedBack" : null, r.ReturnReceived ? "ReturnReceived" : null }.OfType<string>().ToList())).ToList();

        return new QueryResponse<PagedResult<RefundDueOrderViewModel>> { Result = new PagedResult<RefundDueOrderViewModel>(items, pageNumber, pageSize, total) };
    }
}

/// <summary>Worker: unpaid bank-transfer orders whose window has passed and that hold no confirmation (money claimed received is not expired, ADR-204).</summary>
public sealed class GetExpirableOrdersQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider) : IQueryHandlerAsync<GetExpirableOrdersQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetExpirableOrdersQuery query)
    {
        var now = timeProvider.GetUtcNow();
        IReadOnlyList<Guid> ids = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.PendingPayment && o.PaymentMethod == OrderPaymentMethod.BankTransfer && o.PaymentDueBy != null && o.PaymentDueBy <= now && o.HeldPayment == null
                        && (query.BranchId == null || o.BranchId == query.BranchId))
            .OrderBy(o => o.PaymentDueBy)
            .Select(o => o.Id)
            .Take(Math.Clamp(query.Take, 1, 500))
            .ToListAsync();

        return new QueryResponse<IReadOnlyList<Guid>> { Result = ids };
    }
}
