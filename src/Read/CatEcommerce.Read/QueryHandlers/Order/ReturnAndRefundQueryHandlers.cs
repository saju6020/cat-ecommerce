using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Order;

/// <summary>HO-ORD-001 §12.4 (US-ORD-13) -- one return of the caller's, with each line's product, quantity, reason and condition. By id and caller: someone else's return is
/// <c>404</c>.</summary>
public sealed class GetCustomerReturnQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCustomerReturnQuery, CustomerReturnViewModel?>
{
    public async Task<QueryResponse<CustomerReturnViewModel?>> HandleAsync(GetCustomerReturnQuery query)
    {
        var head = await db.ReturnRequests.AsNoTracking().Where(r => r.Id == query.ReturnRequestId && r.CustomerId == query.CustomerId)
            .Select(r => new
            {
                r.Id, r.OrderId, r.Kind, r.Status, r.RequestedOn, r.DecisionNote,
                Lines = r.Lines.Select(l => new { l.OrderLineId, l.Quantity, l.ReasonCode, l.Condition, l.CustomerNote, l.QuantityReceived }).ToList(),
            })
            .FirstOrDefaultAsync();

        if (head is null) return new QueryResponse<CustomerReturnViewModel?> { Result = null };

        var order = await db.Orders.AsNoTracking().Where(o => o.Id == head.OrderId)
            .Select(o => new { o.OrderNumber, Lines = o.Lines.Select(l => new { l.Id, l.ProductName, l.VariantLabel }).ToList() })
            .FirstAsync();

        var lines = head.Lines.Select(l =>
        {
            var named = order.Lines.FirstOrDefault(x => x.Id == l.OrderLineId);
            return new CustomerReturnLineViewModel(l.OrderLineId, named?.ProductName ?? string.Empty, named?.VariantLabel ?? string.Empty, l.Quantity, l.ReasonCode.DisplayName, l.Condition.DisplayName, l.CustomerNote, l.QuantityReceived);
        }).ToList();

        return new QueryResponse<CustomerReturnViewModel?>
        {
            Result = new CustomerReturnViewModel(head.Id, head.OrderId, order.OrderNumber, head.Kind.DisplayName, head.Status.DisplayName, head.RequestedOn, head.DecisionNote, lines)
        };
    }
}

public sealed class GetReturnsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetReturnsQuery, PagedResult<StaffReturnRowViewModel>>
{
    public async Task<QueryResponse<PagedResult<StaffReturnRowViewModel>>> HandleAsync(GetReturnsQuery query)
    {
        var (pageNumber, pageSize) = OrderPaging.Of(query.PageNumber, query.PageSize);
        var branchOrders = db.Orders.AsNoTracking().Where(o => o.BranchId == query.BranchId);
        var returns = db.ReturnRequests.AsNoTracking().Where(r => branchOrders.Any(o => o.Id == r.OrderId));
        if (query.Status is { } status) returns = returns.Where(r => r.Status == status);

        var total = await returns.CountAsync();
        var rows = await returns.OrderByDescending(r => r.RequestedOn).ThenBy(r => r.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, r.OrderId, r.Kind, r.Status, r.RequestedOn, LineCount = r.Lines.Count, Quantity = r.Lines.Sum(l => l.Quantity),
                OrderNumber = db.Orders.Where(o => o.Id == r.OrderId).Select(o => o.OrderNumber).FirstOrDefault(),
                CustomerName = db.Orders.Where(o => o.Id == r.OrderId).Select(o => o.Fulfilment.RecipientName).FirstOrDefault(),
            })
            .ToListAsync();

        var items = rows.Select(r => new StaffReturnRowViewModel(r.Id, r.OrderId, r.OrderNumber ?? string.Empty, r.CustomerName ?? string.Empty, r.Kind.DisplayName, r.Status.DisplayName, r.RequestedOn, r.LineCount, r.Quantity)).ToList();
        return new QueryResponse<PagedResult<StaffReturnRowViewModel>> { Result = new PagedResult<StaffReturnRowViewModel>(items, pageNumber, pageSize, total) };
    }
}

/// <summary>Staff's view of a return: eligibility facts per line (does it need a confirmed defect, was any allocated batch recalled), what came back.</summary>
public sealed class GetReturnDetailQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetReturnDetailQuery, StaffReturnDetailViewModel?>
{
    public async Task<QueryResponse<StaffReturnDetailViewModel?>> HandleAsync(GetReturnDetailQuery query)
    {
        var head = await db.ReturnRequests.AsNoTracking().Where(r => r.Id == query.ReturnRequestId)
            .Select(r => new
            {
                r.Id, r.Version, r.OrderId, r.Kind, r.Status, r.RequestedOn, r.RequestedBy, r.DecidedOn, r.DecidedBy, r.DecisionNote, r.ReceivedOn,
                Lines = r.Lines.Select(l => new
                {
                    l.OrderLineId, l.Quantity, l.ReasonCode, l.Condition, l.CustomerNote, l.RequiresDefectConfirmation, l.DefectConfirmed, l.DefectNote, l.QuantityReceived,
                }).ToList(),
            })
            .FirstOrDefaultAsync();

        if (head is null) return new QueryResponse<StaffReturnDetailViewModel?> { Result = null };

        var order = await db.Orders.AsNoTracking().Where(o => o.Id == head.OrderId && o.BranchId == query.BranchId)
            .Select(o => new
            {
                o.OrderNumber, CustomerName = o.Fulfilment.RecipientName,
                Lines = o.Lines.Select(l => new { l.Id, l.ProductName, l.VariantLabel, Batches = l.Allocations.Select(a => new { a.BatchId, a.LotNumber }).ToList() }).ToList(),
            })
            .FirstOrDefaultAsync();

        if (order is null) return new QueryResponse<StaffReturnDetailViewModel?> { Result = null };

        var batchIds = order.Lines.SelectMany(l => l.Batches).Select(b => b.BatchId).Distinct().ToList();
        var recalled = batchIds.Count == 0
            ? new HashSet<Guid>()
            : (await db.Batches.AsNoTracking().Where(b => batchIds.Contains(b.Id) && b.Status == BatchStatus.Recalled).Select(b => b.Id).ToListAsync()).ToHashSet();

        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";

        var lines = head.Lines.Select(l =>
        {
            var named = order.Lines.FirstOrDefault(x => x.Id == l.OrderLineId);
            return new StaffReturnLineViewModel(
                l.OrderLineId, named?.ProductName ?? string.Empty, named?.VariantLabel ?? string.Empty, l.Quantity, l.ReasonCode.DisplayName, l.Condition.DisplayName, l.CustomerNote,
                l.RequiresDefectConfirmation, l.DefectConfirmed, l.DefectNote, l.QuantityReceived,
                named?.Batches.Any(b => recalled.Contains(b.BatchId)) ?? false, named?.Batches.Select(b => b.LotNumber).Distinct().ToList() ?? []);
        }).ToList();

        return new QueryResponse<StaffReturnDetailViewModel?>
        {
            Result = new StaffReturnDetailViewModel(
                head.Id, head.Version, head.OrderId, order.OrderNumber, order.CustomerName, head.Kind.DisplayName, head.Status.DisplayName, head.RequestedOn, head.RequestedBy,
                head.DecidedOn, head.DecidedBy, head.DecisionNote, head.ReceivedOn, lines, timeZoneId)
        };
    }
}

/// <summary>US-ORD-15 -- the refund screen's facts: the methods by how the customer paid (<see cref="RefundMethodPolicy"/>), the most that can still be refunded, and the amount to
/// suggest for each received return (<see cref="RefundCalculator"/>: the value of the units that came back, never the delivery charge).</summary>
public sealed class GetRefundOptionsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetRefundOptionsQuery, RefundOptionsViewModel?>
{
    public async Task<QueryResponse<RefundOptionsViewModel?>> HandleAsync(GetRefundOptionsQuery query)
    {
        var order = await db.Orders.AsNoTracking().Where(o => o.Id == query.OrderId && o.BranchId == query.BranchId)
            .Select(o => new
            {
                o.Id, o.CurrencyCode, o.AmountPaid, o.AmountRefunded,
                Methods = o.Payments.Select(p => p.Method).ToList(),
                Lines = o.Lines.Select(l => new { l.Id, l.LineTotal, l.Quantity, l.QuantityReturned }).ToList(),
            })
            .FirstOrDefaultAsync();

        if (order is null) return new QueryResponse<RefundOptionsViewModel?> { Result = null };

        var alreadyRefundedReturns = await db.OrderRefunds.AsNoTracking().Where(r => r.OrderId == order.Id && r.ReturnRequestId != null).Select(r => r.ReturnRequestId!.Value).ToListAsync();
        var received = await db.ReturnRequests.AsNoTracking()
            .Where(r => r.OrderId == order.Id && (r.Status == ReturnStatus.Received || r.Status == ReturnStatus.Closed) && !alreadyRefundedReturns.Contains(r.Id))
            .Select(r => new { r.Id, Lines = r.Lines.Select(l => new { l.OrderLineId, l.QuantityReceived }).ToList() })
            .ToListAsync();

        string currency = order.CurrencyCode;
        var suggestions = received.Select(r =>
        {
            var total = RefundCalculator.Suggested(r.Lines.Where(l => l.QuantityReceived > 0).Select(l =>
            {
                var line = order.Lines.First(x => x.Id == l.OrderLineId);
                return new RefundLineInput(MinorUnits.FromAmount(line.LineTotal), line.Quantity, Math.Max(0, line.QuantityReturned - l.QuantityReceived), l.QuantityReceived);
            }));

            return new SuggestedRefundViewModel(r.Id, OrderMoney.Of(total.ToAmount(), currency));
        }).ToList();

        var methods = RefundMethodPolicy.Evaluate(order.Methods, storeCreditAvailable: false)
            .Select(o => new RefundMethodOptionViewModel(o.Method.DisplayName, o.AllowedForPayment, o.Available, o.UnavailableReason)).ToList();

        return new QueryResponse<RefundOptionsViewModel?>
        {
            Result = new RefundOptionsViewModel(order.Id, methods, OrderMoney.Of(Math.Max(0m, order.AmountPaid - order.AmountRefunded), currency), suggestions)
        };
    }
}
