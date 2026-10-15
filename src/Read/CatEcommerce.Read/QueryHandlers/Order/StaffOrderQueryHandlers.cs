using System.Globalization;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Order;

/// <summary>HO-ORD-001 §12.4 -- the staff order queue: a paged projection with the highlights staff act on (<c>flags[]</c>).</summary>
public sealed class GetStaffOrderQueueQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetStaffOrderQueueQuery, PagedResult<StaffOrderRowViewModel>>
{
    public async Task<QueryResponse<PagedResult<StaffOrderRowViewModel>>> HandleAsync(GetStaffOrderQueueQuery query)
    {
        var (pageNumber, pageSize) = OrderPaging.Of(query.PageNumber, query.PageSize);
        var orders = db.Orders.AsNoTracking().Where(o => o.BranchId == query.BranchId);

        if (query.Status is { } status) orders = orders.Where(o => o.Status == status);
        if (query.Fulfilment is { } fulfilment) orders = orders.Where(o => o.FulfilmentMethod == fulfilment);
        if (query.PaymentMethod is { } payment) orders = orders.Where(o => o.PaymentMethod == payment);
        if (query.Channel is { } channel) orders = orders.Where(o => o.Channel == channel);
        if (query.From is { } from) orders = orders.Where(o => o.PlacedOn >= from);
        if (query.To is { } to) orders = orders.Where(o => o.PlacedOn <= to);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = "%" + query.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            orders = orders.Where(o => EF.Functions.ILike(o.OrderNumber, pattern) || EF.Functions.ILike(o.Fulfilment.RecipientName, pattern)
                || (o.Fulfilment.RecipientPhone != null && EF.Functions.ILike(o.Fulfilment.RecipientPhone, pattern)));
        }

        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).DateTime);

        var total = await orders.CountAsync();
        var rows = await orders.OrderByDescending(o => o.PlacedOn).ThenBy(o => o.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.PlacedOn, CustomerName = o.Fulfilment.RecipientName, o.Status, o.FulfilmentMethod, o.PaymentMethod, o.Channel, o.CurrencyCode,
                o.Totals.GrandTotal, Held = o.HeldPayment != null, o.CustomerTransferReference, o.CollectByDate, o.CustomerAnonymised,
                TrackingStatus = o.Shipment != null ? o.Shipment.LastTrackingStatus : null,
            })
            .ToListAsync();

        var items = rows.Select(r =>
        {
            var flags = new List<string>();
            if (r.Held) flags.Add("AwaitingApproval");
            if (r.Status == OrderStatus.PendingPayment && r.CustomerTransferReference != null) flags.Add("TransferReported");
            if (r.Status == OrderStatus.Dispatched && r.TrackingStatus != null && r.TrackingStatus.Contains("deliver", StringComparison.OrdinalIgnoreCase)) flags.Add("CourierReportsDelivered");
            if (r.Status == OrderStatus.Dispatched && r.FulfilmentMethod == FulfilmentMethod.Pickup && r.CollectByDate is { } due && due < today) flags.Add("CollectionOverdue");
            if (r.CustomerAnonymised) flags.Add("CustomerAnonymised");

            return new StaffOrderRowViewModel(
                r.Id, r.OrderNumber, r.PlacedOn, r.CustomerName, r.Status.DisplayName, r.FulfilmentMethod.DisplayName, r.PaymentMethod.DisplayName, r.Channel.DisplayName,
                OrderMoney.Of(r.GrandTotal, r.CurrencyCode), (long)Math.Max(0, (now - r.PlacedOn).TotalMinutes), flags, timeZoneId);
        }).ToList();

        return new QueryResponse<PagedResult<StaffOrderRowViewModel>> { Result = new PagedResult<StaffOrderRowViewModel>(items, pageNumber, pageSize, total) };
    }
}

/// <summary>HO-ORD-001 §12.4 -- the full staff view of one order: allocations per line (batch number, expiry, location), the history with who acted, a held
/// payment, the cash collected, the payments and credit notes, and <c>allowedActions[]</c> -- the transitions the table allows from this state, narrowed to the
/// ones the caller's roles may perform.</summary>
public sealed class GetStaffOrderDetailQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStaffOrderDetailQuery, StaffOrderDetailViewModel?>
{
    public async Task<QueryResponse<StaffOrderDetailViewModel?>> HandleAsync(GetStaffOrderDetailQuery query)
    {
        var head = await db.Orders.AsNoTracking().Where(o => o.Id == query.OrderId && o.BranchId == query.BranchId)
            .Select(o => new
            {
                o.Id, o.Version, o.OrderNumber, o.PlacedOn, o.Channel, o.CustomerId, o.Status, o.FulfilmentMethod, o.PaymentMethod, o.CurrencyCode, o.PaymentDueBy,
                o.CustomerTransferReference, o.CollectByDate, o.InvoiceId, o.InvoiceNumber, o.CreditNoteIds, o.AmountPaid, o.AmountRefunded, o.RefundDue, o.DiscountCodeText,
                o.CustomerAnonymised,
                Totals = new { o.Totals.Subtotal, o.Totals.LineDiscountTotal, o.Totals.OrderDiscount, o.Totals.DeliveryCharge, o.Totals.TaxTotal, o.Totals.GrandTotal },
                F = new
                {
                    o.Fulfilment.RecipientName, o.Fulfilment.RecipientPhone, o.Fulfilment.Line1, o.Fulfilment.Line2, o.Fulfilment.City, o.Fulfilment.District, o.Fulfilment.PostalCode,
                    o.Fulfilment.DeliveryInstructions, o.Fulfilment.ZoneName, o.Fulfilment.ServiceLevel, o.Fulfilment.SlotDate, o.Fulfilment.SlotWindowLabel, o.Fulfilment.PickupBranchId,
                },
                Held = o.HeldPayment == null ? null : new { o.HeldPayment.Amount, o.HeldPayment.RecordedBy, o.HeldPayment.RecordedOn },
                Cod = o.CodCollection == null ? null : new { o.CodCollection.Amount, o.CodCollection.CollectorKind, o.CodCollection.CollectorName, o.CodCollection.CollectedOn, o.CodCollection.RecordedBy },
                Ship = o.Shipment == null ? null : new
                {
                    o.Shipment.CourierId, o.Shipment.CourierName, o.Shipment.TrackingNumber, o.Shipment.TrackingUrl, o.Shipment.DispatchedOn, o.Shipment.LastTrackingStatus,
                    o.Shipment.LastTrackingEventOn,
                },
                Lines = o.Lines.OrderBy(l => l.LineNumber).Select(l => new
                {
                    l.Id, l.LineNumber, l.SkuId, l.ProductName, l.VariantLabel, l.CatId, l.Quantity, l.UnitPrice, l.LineTotal, l.QuantityReturned,
                    Allocations = l.Allocations.Select(a => new { a.BatchId, a.LotNumber, a.BatchExpiresOn, a.LocationId, a.Quantity }).ToList(),
                }).ToList(),
                History = o.StatusHistory.OrderBy(h => h.ChangedOn).Select(h => new { h.From, h.To, h.ChangedOn, h.ChangedBy, h.Note }).ToList(),
                Payments = o.Payments.OrderBy(p => p.RecordedOn).Select(p => new { p.PaymentId, p.Method, p.Amount, p.RecordedOn }).ToList(),
            })
            .FirstOrDefaultAsync();

        if (head is null) return new QueryResponse<StaffOrderDetailViewModel?> { Result = null };

        string currency = head.CurrencyCode;
        MoneyViewModel Money(decimal amount) => OrderMoney.Of(amount, currency);

        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var locationIds = head.Lines.SelectMany(l => l.Allocations).Select(a => a.LocationId).Distinct().ToList();
        var locations = locationIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.StockLocations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).Select(l => new { l.Id, l.Name }).ToDictionaryAsync(l => l.Id, l => l.Name);

        var actorIds = head.History.Select(h => h.ChangedBy).Distinct().ToList();
        var staffNames = await db.Set<StaffAccountAggregate>().AsNoTracking().Where(s => actorIds.Contains(s.Id)).Select(s => new { s.Id, s.FullName }).ToDictionaryAsync(s => s.Id, s => s.FullName);

        var catIds = head.Lines.Where(l => l.CatId != null).Select(l => l.CatId!.Value).Distinct().ToList();
        var cats = catIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<CatAggregate>().AsNoTracking().Where(c => catIds.Contains(c.Id)).Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name);

        string? pickupName = null;
        string? pickupAddress = null;
        if (head.F.PickupBranchId is { } pickupId)
        {
            var pickup = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == pickupId).Select(b => new { b.Name, b.AddressLine }).FirstOrDefaultAsync();
            pickupName = pickup?.Name;
            pickupAddress = pickup?.AddressLine;
        }

        var allowed = OrderTransitionRules.AllowedActions(head.Status, head.FulfilmentMethod, head.PaymentMethod)
            .Where(a => RequiredAction(a) is { } action && ActionRoles.TryGetRoles(action, out var roles) && roles.Intersect(query.UserContext?.Roles ?? [], StringComparer.Ordinal).Any())
            .Select(a => a.ToString()).ToList();

        var view = new StaffOrderDetailViewModel(
            head.Id, head.Version, head.OrderNumber, head.PlacedOn, head.Channel.DisplayName, head.CustomerId, head.Status.DisplayName, head.FulfilmentMethod.DisplayName,
            head.PaymentMethod.DisplayName, timeZoneId,
            head.Lines.Select(l => new StaffOrderLineViewModel(
                l.Id, l.LineNumber, l.SkuId, l.ProductName, l.VariantLabel, l.CatId is { } c && cats.TryGetValue(c, out var catName) ? new CartCatViewModel(c, catName) : null, l.Quantity,
                Money(l.UnitPrice), Money(l.LineTotal), l.QuantityReturned,
                l.Allocations.Select(a => new StaffAllocationViewModel(a.BatchId, a.LotNumber, a.BatchExpiresOn, a.LocationId, locations.GetValueOrDefault(a.LocationId, string.Empty), a.Quantity)).ToList())).ToList(),
            new OrderTotalsViewModel(
                Money(head.Totals.Subtotal), Money(head.Totals.LineDiscountTotal), Money(head.Totals.OrderDiscount), Money(head.Totals.DeliveryCharge), Money(head.Totals.TaxTotal), Money(head.Totals.GrandTotal)),
            new OrderFulfilmentViewModel(
                head.F.RecipientName, head.F.RecipientPhone, head.F.Line1, head.F.Line2, head.F.City, head.F.District, head.F.PostalCode, head.F.DeliveryInstructions, head.F.ZoneName,
                head.F.ServiceLevel?.DisplayName, head.F.SlotDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), head.F.SlotWindowLabel, head.F.PickupBranchId, pickupName, pickupAddress),
            head.History.Select(h => new StaffStatusChangeViewModel(h.From?.DisplayName, h.To.DisplayName, h.ChangedOn, h.ChangedBy, staffNames.GetValueOrDefault(h.ChangedBy), h.Note)).ToList(),
            head.PaymentDueBy, head.CustomerTransferReference,
            head.Held is null ? null : new HeldPaymentViewModel(Money(head.Held.Amount), head.Held.RecordedBy, head.Held.RecordedOn),
            head.Cod is null ? null : new StaffCodCollectionViewModel(Money(head.Cod.Amount), head.Cod.CollectorKind.DisplayName, head.Cod.CollectorName, head.Cod.CollectedOn, head.Cod.RecordedBy),
            head.Ship is null ? null : new StaffShipmentViewModel(head.Ship.CourierId, head.Ship.CourierName, head.Ship.TrackingNumber, head.Ship.TrackingUrl, head.Ship.DispatchedOn, head.Ship.LastTrackingStatus, head.Ship.LastTrackingEventOn),
            head.CollectByDate, head.InvoiceId, head.InvoiceNumber,
            head.Payments.Select(p => new StaffPaymentRefViewModel(p.PaymentId, p.Method, Money(p.Amount), p.RecordedOn)).ToList(), head.CreditNoteIds,
            Money(head.AmountPaid), Money(head.AmountRefunded), Money(head.RefundDue), head.DiscountCodeText, head.CustomerAnonymised, allowed);

        return new QueryResponse<StaffOrderDetailViewModel?> { Result = view };
    }

    private static string? RequiredAction(OrderTransitionAction action) => action switch
    {
        OrderTransitionAction.ConfirmPayment => Actions.Orders.ConfirmPayment,
        OrderTransitionAction.Cancel => Actions.Orders.Cancel,
        OrderTransitionAction.StartPacking or OrderTransitionAction.Dispatch or OrderTransitionAction.MarkReadyForCollection or OrderTransitionAction.RecordDeliveryFailure => Actions.Orders.Fulfil,
        OrderTransitionAction.RecordDelivery => Actions.Orders.RecordDelivery,
        OrderTransitionAction.Refund => Actions.Refunds.Issue,
        _ => null, // Expire is the worker's; CompleteReturn and CollectAtCounter are driven by other flows, never chosen
    };
}

/// <summary>HO-ORD-001 §12.4, US-ORD-10 AC1 -- the pick list: the allocations of the chosen orders (or of every <c>Confirmed</c> / <c>Packing</c> order of the branch)
/// grouped by location, then batch, with the quantity each order needs.</summary>
public sealed class GetPickListQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider) : IQueryHandlerAsync<GetPickListQuery, PickListViewModel>
{
    public async Task<QueryResponse<PickListViewModel>> HandleAsync(GetPickListQuery query)
    {
        var orders = db.Orders.AsNoTracking().Where(o => o.BranchId == query.BranchId);
        orders = query.OrderIds is { Count: > 0 } ids
            ? orders.Where(o => ids.Contains(o.Id) && (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Packing))
            : orders.Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Packing);

        var rows = await orders.SelectMany(o => o.Lines.SelectMany(l => l.Allocations.Select(a => new
        {
            o.OrderNumber, l.ProductName, l.VariantLabel, a.SkuId, a.BatchId, a.LotNumber, a.BatchExpiresOn, a.LocationId, a.Quantity,
        }))).Take(5000).ToListAsync();

        var locationIds = rows.Select(r => r.LocationId).Distinct().ToList();
        var locations = locationIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.StockLocations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).Select(l => new { l.Id, l.Name }).ToDictionaryAsync(l => l.Id, l => l.Name);

        var groups = rows.GroupBy(r => r.LocationId).OrderBy(g => locations.GetValueOrDefault(g.Key, string.Empty)).Select(location =>
            new PickListGroupViewModel(
                location.Key, locations.GetValueOrDefault(location.Key, string.Empty),
                location.GroupBy(r => r.BatchId).OrderBy(b => b.First().BatchExpiresOn ?? DateOnly.MaxValue).ThenBy(b => b.First().LotNumber).Select(batch =>
                {
                    var first = batch.First();
                    return new PickListBatchViewModel(
                        batch.Key, first.LotNumber, first.BatchExpiresOn, first.SkuId, first.ProductName, first.VariantLabel, batch.Sum(r => r.Quantity),
                        batch.GroupBy(r => r.OrderNumber).OrderBy(o => o.Key, StringComparer.Ordinal).Select(o => new PickListOrderViewModel(o.Key, (int)o.Sum(r => r.Quantity))).ToList());
                }).ToList())).ToList();

        return new QueryResponse<PickListViewModel> { Result = new PickListViewModel(timeProvider.GetUtcNow(), groups) };
    }
}

/// <summary>HO-ORD-001 §12.4, US-ORD-10 AC2 -- the packing slip: contents with batch numbers and the customer's details; the COD amount to collect on a COD order.</summary>
public sealed class GetPackingSlipQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetPackingSlipQuery, PackingSlipViewModel?>
{
    public async Task<QueryResponse<PackingSlipViewModel?>> HandleAsync(GetPackingSlipQuery query)
    {
        var order = await db.Orders.AsNoTracking().Where(o => o.Id == query.OrderId && o.BranchId == query.BranchId)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.PlacedOn, o.FulfilmentMethod, o.PaymentMethod, o.CurrencyCode, o.AmountPaid, o.Totals.GrandTotal,
                F = new
                {
                    o.Fulfilment.RecipientName, o.Fulfilment.RecipientPhone, o.Fulfilment.Line1, o.Fulfilment.Line2, o.Fulfilment.City, o.Fulfilment.District, o.Fulfilment.PostalCode,
                    o.Fulfilment.DeliveryInstructions, o.Fulfilment.SlotDate, o.Fulfilment.SlotWindowLabel, o.Fulfilment.PickupBranchId,
                },
                Lines = o.Lines.OrderBy(l => l.LineNumber).Select(l => new { l.LineNumber, l.ProductName, l.VariantLabel, l.Quantity, l.CatId, Batches = l.Allocations.Select(a => a.LotNumber).ToList() }).ToList(),
            })
            .FirstOrDefaultAsync();

        if (order is null) return new QueryResponse<PackingSlipViewModel?> { Result = null };

        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var catIds = order.Lines.Where(l => l.CatId != null).Select(l => l.CatId!.Value).Distinct().ToList();
        var cats = catIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<CatAggregate>().AsNoTracking().Where(c => catIds.Contains(c.Id)).Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name);

        string? pickupName = null;
        if (order.F.PickupBranchId is { } pickupId)
            pickupName = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == pickupId).Select(b => b.Name).FirstOrDefaultAsync();

        var address = string.Join(", ", new[] { order.F.Line1, order.F.Line2, order.F.City, order.F.District, order.F.PostalCode }.Where(p => !string.IsNullOrWhiteSpace(p)));
        var codDue = order.PaymentMethod == OrderPaymentMethod.CashOnDelivery ? Math.Max(0m, order.GrandTotal - order.AmountPaid) : (decimal?)null;

        var view = new PackingSlipViewModel(
            order.Id, order.OrderNumber, order.PlacedOn, order.F.RecipientName, order.F.RecipientName, order.F.RecipientPhone, order.FulfilmentMethod.DisplayName,
            string.IsNullOrEmpty(address) ? null : address, pickupName,
            order.F.SlotDate is { } date ? $"{date:yyyy-MM-dd} {order.F.SlotWindowLabel}".Trim() : null,
            order.Lines.Select(l => new PackingSlipLineViewModel(l.LineNumber, l.ProductName, l.VariantLabel, l.Quantity, l.CatId is { } c ? cats.GetValueOrDefault(c) : null, l.Batches)).ToList(),
            codDue is { } due ? OrderMoney.Of(due, order.CurrencyCode) : null, order.F.DeliveryInstructions, timeZoneId);

        return new QueryResponse<PackingSlipViewModel?> { Result = view };
    }
}

/// <summary>HO-ORD-001 §12.4, US-ORD-11 AC2 -- the CSV manifest rows: orders dispatched with a courier on a branch-local date, with the COD amount to collect.</summary>
public sealed class GetCourierManifestQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCourierManifestQuery, IReadOnlyList<ManifestRowViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<ManifestRowViewModel>>> HandleAsync(GetCourierManifestQuery query)
    {
        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var start = new DateTimeOffset(query.Date.ToDateTime(TimeOnly.MinValue), zone.GetUtcOffset(query.Date.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();
        var end = new DateTimeOffset(query.Date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone.GetUtcOffset(query.Date.AddDays(1).ToDateTime(TimeOnly.MinValue))).ToUniversalTime();

        var rows = await db.Orders.AsNoTracking()
            .Where(o => o.BranchId == query.BranchId && o.Shipment != null && o.Shipment.CourierId == query.CourierId && o.Shipment.DispatchedOn >= start && o.Shipment.DispatchedOn < end)
            .OrderBy(o => o.Shipment!.DispatchedOn)
            .Select(o => new
            {
                o.OrderNumber, o.Fulfilment.RecipientName, o.Fulfilment.RecipientPhone, o.Fulfilment.Line1, o.Fulfilment.Line2, o.Fulfilment.City, o.Fulfilment.District,
                o.Fulfilment.PostalCode, o.PaymentMethod, o.AmountPaid, o.Totals.GrandTotal, TrackingNumber = o.Shipment!.TrackingNumber,
            })
            .Take(2000)
            .ToListAsync();

        IReadOnlyList<ManifestRowViewModel> result = rows.Select(r => new ManifestRowViewModel(
            r.OrderNumber, r.RecipientName, r.RecipientPhone, string.Join(", ", new[] { r.Line1, r.Line2, r.City }.Where(p => !string.IsNullOrWhiteSpace(p))), r.District, r.PostalCode,
            1, 0, r.PaymentMethod == OrderPaymentMethod.CashOnDelivery ? Math.Max(0m, r.GrandTotal - r.AmountPaid) : 0m, r.TrackingNumber)).ToList();

        return new QueryResponse<IReadOnlyList<ManifestRowViewModel>> { Result = result };
    }
}

/// <summary>HO-ORD-001 §12.4 -- the orders of one customer account at the caller's active branch (the same branch rule as the queue). A paged, untracked projection.</summary>
public sealed class GetStaffCustomerOrdersQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetStaffCustomerOrdersQuery, PagedResult<StaffCustomerOrderRowViewModel>?>
{
    public async Task<QueryResponse<PagedResult<StaffCustomerOrderRowViewModel>?>> HandleAsync(GetStaffCustomerOrdersQuery query)
    {
        if (!await db.CustomerAccounts.AsNoTracking().AnyAsync(c => c.Id == query.CustomerId))
            return new QueryResponse<PagedResult<StaffCustomerOrderRowViewModel>?> { Result = null };

        var (pageNumber, pageSize) = OrderPaging.Of(query.PageNumber, query.PageSize);
        var orders = db.Orders.AsNoTracking().Where(o => o.BranchId == query.BranchId && o.CustomerId == query.CustomerId);

        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var total = await orders.CountAsync();
        var rows = await orders.OrderByDescending(o => o.PlacedOn).ThenBy(o => o.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(o => new { o.Id, o.OrderNumber, o.PlacedOn, o.Status, o.FulfilmentMethod, o.PaymentMethod, o.Channel, o.CurrencyCode, o.Totals.GrandTotal })
            .ToListAsync();

        var items = rows.Select(r => new StaffCustomerOrderRowViewModel(
            r.Id, r.OrderNumber, r.PlacedOn, r.Status.DisplayName, r.FulfilmentMethod.DisplayName, r.PaymentMethod.DisplayName, r.Channel.DisplayName,
            OrderMoney.Of(r.GrandTotal, r.CurrencyCode), timeZoneId)).ToList();

        return new QueryResponse<PagedResult<StaffCustomerOrderRowViewModel>?> { Result = new PagedResult<StaffCustomerOrderRowViewModel>(items, pageNumber, pageSize, total) };
    }
}
