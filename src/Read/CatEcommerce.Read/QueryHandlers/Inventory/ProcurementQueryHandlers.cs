using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Inventory;

/// <summary>HO-IMP-001 §12.4 -- suppliers. Filtering, sorting and paging happen in SQL (UI-4).</summary>
public sealed class GetSuppliersQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetSuppliersQuery, PagedResult<SupplierSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<SupplierSummaryViewModel>>> HandleAsync(GetSuppliersQuery query)
    {
        var filtered = db.Suppliers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(s => s.Name.ToLower().Contains(term) || s.Code.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.CountryCode))
        {
            var country = query.CountryCode.Trim().ToUpperInvariant();
            filtered = filtered.Where(s => s.CountryCode == country);
        }

        if (query.IsActive is { } active) filtered = filtered.Where(s => s.IsActive == active);

        var totalCount = await filtered.CountAsync();
        var items = await filtered.OrderBy(s => s.Name).ThenBy(s => s.Code)
            .Skip(query.Skip).Take(query.Take)
            .Select(s => new SupplierSummaryViewModel(s.Id, s.Code, s.Name, s.CountryCode, s.CurrencyCode, s.LeadTimeDays, s.PaymentTerms, s.IsActive))
            .ToListAsync();

        return new QueryResponse<PagedResult<SupplierSummaryViewModel>>
        { Result = new PagedResult<SupplierSummaryViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}

public sealed class GetSupplierQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetSupplierQuery, SupplierDetailViewModel?>
{
    public async Task<QueryResponse<SupplierDetailViewModel?>> HandleAsync(GetSupplierQuery query)
    {
        var supplier = await db.Suppliers.AsNoTracking()
            .Where(s => s.Id == query.SupplierId)
            .Select(s => new SupplierDetailViewModel(
                s.Id, s.Code, s.Name, s.CountryCode, s.Contact.ContactName, s.Contact.Phone, s.Contact.Email, s.Contact.Address,
                s.CurrencyCode, s.LeadTimeDays, s.PaymentTerms, s.PaymentTermDays, s.Notes, s.IsActive, s.Version))
            .FirstOrDefaultAsync();

        return new QueryResponse<SupplierDetailViewModel?> { Result = supplier };
    }
}

/// <summary>HO-IMP-001 §12.4 -- the receipt state is DERIVED from posted receipts (ADR-112) and filtered here, never stored.</summary>
public sealed class GetPurchaseOrdersQueryHandler(CatEcommerceDbContext db, IStockPositionReader positions)
    : IQueryHandlerAsync<GetPurchaseOrdersQuery, PagedResult<PurchaseOrderSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<PurchaseOrderSummaryViewModel>>> HandleAsync(GetPurchaseOrdersQuery query)
    {
        var filtered = db.PurchaseOrders.AsNoTracking().Where(p => p.BranchId == query.BranchId);

        if (EnumerationParsing.TryParse<PurchaseOrderStatus>(query.Status, out var status) && status is not null)
            filtered = filtered.Where(p => p.Status == status);
        if (query.SupplierId is { } supplierId) filtered = filtered.Where(p => p.SupplierId == supplierId);
        if (query.From is { } from) filtered = filtered.Where(p => p.ExpectedDeliveryOn >= from);
        if (query.To is { } to) filtered = filtered.Where(p => p.ExpectedDeliveryOn <= to);

        if (EnumerationParsing.TryParse<PurchaseOrderReceiptState>(query.ReceiptState, out var wanted) && wanted is not null)
        {
            // Derived, so filtered over the candidates after the SQL filters above: one grouped read, then the page.
            var candidateIds = await filtered.Select(p => p.Id).ToListAsync();
            var states = await positions.GetPurchaseOrderReceiptStatesAsync(candidateIds, CancellationToken.None);
            var matching = candidateIds.Where(id => states.GetValueOrDefault(id, PurchaseOrderReceiptState.NotReceived) == wanted).ToList();
            filtered = filtered.Where(p => matching.Contains(p.Id));
        }

        var totalCount = await filtered.CountAsync();
        var rows = await filtered.OrderByDescending(p => p.Number)
            .Skip(query.Skip).Take(query.Take)
            .Select(p => new
            {
                p.Id, p.Number, p.SupplierId, p.Status, p.ExpectedDeliveryOn, p.CurrencyCode,
                Total = p.Lines.Sum(l => l.OrderedQuantity * l.UnitCostAmount)
            })
            .ToListAsync();

        var supplierIds = rows.Select(r => r.SupplierId).Distinct().ToList();
        var supplierNames = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).Select(s => new { Key = s.Id, Value = s.Name }).ToDictionaryAsync(x => x.Key, x => x.Value);
        var receiptStates = await positions.GetPurchaseOrderReceiptStatesAsync(rows.Select(r => r.Id).ToList(), CancellationToken.None);

        var items = rows.Select(r => new PurchaseOrderSummaryViewModel(
            r.Id, r.Number, r.SupplierId, supplierNames.GetValueOrDefault(r.SupplierId, string.Empty), r.Status.DisplayName,
            receiptStates.GetValueOrDefault(r.Id, PurchaseOrderReceiptState.NotReceived).DisplayName, r.ExpectedDeliveryOn,
            InventoryViewFormat.Money(decimal.Round(r.Total, 2, MidpointRounding.ToEven), r.CurrencyCode))).ToList();

        return new QueryResponse<PagedResult<PurchaseOrderSummaryViewModel>>
        { Result = new PagedResult<PurchaseOrderSummaryViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}

/// <summary>HO-IMP-001 §12.4 / US-IMP-02 AC2 -- per line <c>orderedQuantity</c>, <c>acceptedQuantity</c>, <c>rejectedQuantity</c> and
/// <c>outstandingQuantity</c> (all derived by <see cref="PurchaseOrderProgress"/>), the receipt state, the receipts and the actions allowed now.</summary>
public sealed class GetPurchaseOrderQueryHandler(CatEcommerceDbContext db, IStockPositionReader positions)
    : IQueryHandlerAsync<GetPurchaseOrderQuery, PurchaseOrderDetailViewModel?>
{
    public async Task<QueryResponse<PurchaseOrderDetailViewModel?>> HandleAsync(GetPurchaseOrderQuery query)
    {
        var order = await db.PurchaseOrders.AsNoTracking()
            .Where(p => p.Id == query.PurchaseOrderId && p.BranchId == query.BranchId)
            .Select(p => new
            {
                p.Id, p.Number, p.SupplierId, p.BranchId, p.DeliverToLocationId, p.CurrencyCode, p.ExpectedDeliveryOn, p.Notes, p.Status, p.StatusReason,
                p.IssuedOn, p.Version,
                Lines = p.Lines.Select(l => new { l.Id, l.SkuId, l.SkuCode, l.DisplayName, l.StockUnit, l.OrderedQuantity, l.UnitCostAmount }).ToList()
            })
            .FirstOrDefaultAsync();

        if (order is null) return new QueryResponse<PurchaseOrderDetailViewModel?> { Result = null };

        var supplierName = await db.Suppliers.AsNoTracking().Where(s => s.Id == order.SupplierId).Select(s => s.Name).FirstOrDefaultAsync() ?? string.Empty;
        var receipts = (await positions.GetPurchaseOrderReceiptsAsync(order.Id, CancellationToken.None)).ToDictionary(r => r.PurchaseOrderLineId);
        var headers = await positions.GetPurchaseOrderReceiptHeadersAsync(order.Id, CancellationToken.None);

        var progress = order.Lines.Select(l =>
        {
            var received = receipts.GetValueOrDefault(l.Id);
            return PurchaseOrderProgress.ForLine(l.OrderedQuantity, received?.Accepted ?? 0m, received?.Rejected ?? 0m);
        }).ToList();

        var lines = order.Lines.Select((l, i) => new PurchaseOrderLineViewModel(
            l.Id, l.SkuId, l.SkuCode, l.DisplayName, l.StockUnit,
            InventoryViewFormat.Quantity(progress[i].Ordered), InventoryViewFormat.Quantity(progress[i].Accepted),
            InventoryViewFormat.Quantity(progress[i].Rejected), InventoryViewFormat.Quantity(progress[i].Outstanding),
            InventoryViewFormat.UnitCost(l.UnitCostAmount, order.CurrencyCode),
            InventoryViewFormat.Money(decimal.Round(l.OrderedQuantity * l.UnitCostAmount, 2, MidpointRounding.ToEven), order.CurrencyCode))).ToList();

        var total = order.Lines.Sum(l => l.OrderedQuantity * l.UnitCostAmount);
        var state = PurchaseOrderProgress.StateOf(progress);

        return new QueryResponse<PurchaseOrderDetailViewModel?>
        {
            Result = new PurchaseOrderDetailViewModel(
                order.Id, order.Number, order.SupplierId, supplierName, order.BranchId, order.DeliverToLocationId, order.CurrencyCode,
                order.ExpectedDeliveryOn, order.Notes, order.Status.DisplayName, order.StatusReason, state.DisplayName, order.IssuedOn,
                InventoryViewFormat.Money(decimal.Round(total, 2, MidpointRounding.ToEven), order.CurrencyCode), lines,
                headers.Select(h => new PurchaseOrderReceiptViewModel(h.GoodsReceiptId, h.Number, h.ReceivedOn)).ToList(),
                AllowedActionsFor(order.Status, state), order.Version)
        };
    }

    /// <summary>What the staff screen may offer now: a draft is edited, issued or cancelled; an issued order is short-closed, and cancelled
    /// only while nothing has been received (the aggregate and handler decide again on the write).</summary>
    private static IReadOnlyList<string> AllowedActionsFor(PurchaseOrderStatus status, PurchaseOrderReceiptState state)
    {
        if (status == PurchaseOrderStatus.Draft) return ["edit", "issue", "cancel"];
        if (status == PurchaseOrderStatus.Issued)
            return state == PurchaseOrderReceiptState.NotReceived ? ["receive", "close", "cancel"] : ["receive", "close"];
        return [];
    }
}
