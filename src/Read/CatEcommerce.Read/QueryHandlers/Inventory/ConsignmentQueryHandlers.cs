using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Inventory;

/// <summary>HO-IMP-001 §12.4 -- consignments of the active branch; filtering, sorting and paging in SQL (UI-4).</summary>
public sealed class GetConsignmentsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetConsignmentsQuery, PagedResult<ConsignmentSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<ConsignmentSummaryViewModel>>> HandleAsync(GetConsignmentsQuery query)
    {
        var filtered = db.Consignments.AsNoTracking().Where(c => c.BranchId == query.BranchId);

        if (EnumerationParsing.TryParse<ConsignmentStatus>(query.Status, out var status) && status is not null)
            filtered = filtered.Where(c => c.Status == status);
        if (!string.IsNullOrWhiteSpace(query.OriginCountryCode))
        {
            var country = query.OriginCountryCode.Trim().ToUpperInvariant();
            filtered = filtered.Where(c => c.OriginCountryCode == country);
        }

        // From/To bound the day the consignment was planned (UTC), the only date every consignment has.
        if (query.From is { } from) filtered = filtered.Where(c => c.CreatedDate >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (query.To is { } to) filtered = filtered.Where(c => c.CreatedDate < to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var totalCount = await filtered.CountAsync();
        var rows = await filtered.OrderByDescending(c => c.Reference)
            .Skip(query.Skip).Take(query.Take)
            .Select(c => new { c.Id, c.Reference, c.Status, c.OriginCountryCode, c.ShipmentReference, c.ExpectedArrivalOn, PurchaseOrderIds = c.PurchaseOrderIds })
            .ToListAsync();

        var items = rows.Select(r => new ConsignmentSummaryViewModel(
            r.Id, r.Reference, r.Status.DisplayName, r.OriginCountryCode, r.ShipmentReference, r.ExpectedArrivalOn, r.PurchaseOrderIds.Count)).ToList();

        return new QueryResponse<PagedResult<ConsignmentSummaryViewModel>>
        { Result = new PagedResult<ConsignmentSummaryViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}

/// <summary>The pieces the detail and the inspection pack share -- one place, so the two cannot disagree about what a consignment holds.</summary>
internal static class ConsignmentReads
{
    public static async Task<IReadOnlyList<ConsignmentPurchaseOrderViewModel>> PurchaseOrdersAsync(CatEcommerceDbContext db, IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0) return [];

        var orderIds = ids.ToList();
        var orders = await db.PurchaseOrders.AsNoTracking().Where(p => orderIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Number, p.SupplierId, p.Status }).ToListAsync();
        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).Select(s => new { Key = s.Id, Value = s.Name }).ToDictionaryAsync(x => x.Key, x => x.Value);

        return orders.OrderBy(o => o.Number)
            .Select(o => new ConsignmentPurchaseOrderViewModel(o.Id, o.Number, o.SupplierId, suppliers.GetValueOrDefault(o.SupplierId, string.Empty), o.Status.DisplayName))
            .ToList();
    }

    public static ConsignmentDocumentViewModel ToViewModel(
        Guid id, ImportDocumentKind kind, string? number, DateOnly? issuedOn, string fileName, string storageKey, string contentType, long size,
        Guid uploadedBy, DateTimeOffset uploadedOn, DateTimeOffset? supersededOn, Guid? supersededBy) =>
        new(id, kind.DisplayName, number, issuedOn, fileName, storageKey, contentType, size, uploadedBy, uploadedOn, supersededOn is not null, supersededOn, supersededBy);

    /// <summary>The allocation (§8.11): its lines with the SKU and lot, the total allocated, and whether it is STALE -- the costs changed since, or the batches
    /// received on the consignment are no longer the batches the allocation covered.</summary>
    public static async Task<LandedCostViewModel> LandedCostAsync(CatEcommerceDbContext db, Guid consignmentId, DateTimeOffset? allocatedOn, bool costsChanged, string baseCurrency)
    {
        if (allocatedOn is null) return new LandedCostViewModel(null, false, null, []);

        var lines = await (
            from line in db.Consignments.AsNoTracking().Where(x => x.Id == consignmentId).SelectMany(x => x.LandedCost)
            join b in db.Batches.AsNoTracking() on line.BatchId equals b.Id into batches
            from b in batches.DefaultIfEmpty()
            orderby b.ReceivedOn, b.LotNumber
            select new { line.BatchId, SkuCode = b.SkuCode, Lot = b.LotNumber, line.Quantity, line.PurchaseCostBase, line.AllocatedCostBase, line.LandedUnitCostBase }).ToListAsync();

        var receivedNow = await db.Batches.AsNoTracking().Where(b => b.ConsignmentId == consignmentId && b.ReceivedQuantity > 0m).Select(b => b.Id).ToListAsync();
        var isStale = costsChanged || !receivedNow.ToHashSet().SetEquals(lines.Select(l => l.BatchId));

        return new LandedCostViewModel(
            allocatedOn, isStale, InventoryViewFormat.Money(lines.Sum(l => l.AllocatedCostBase), baseCurrency),
            lines.Select(l => new LandedCostLineViewModel(
                l.BatchId, l.SkuCode, l.Lot, InventoryViewFormat.Quantity(l.Quantity), InventoryViewFormat.Money(l.PurchaseCostBase, baseCurrency),
                InventoryViewFormat.Money(l.AllocatedCostBase, baseCurrency), InventoryViewFormat.UnitCost(l.LandedUnitCostBase, baseCurrency))).ToList());
    }

    /// <summary>What the staff screen may offer next; the aggregate decides again on every write.</summary>
    public static IReadOnlyList<string> AllowedActions(ConsignmentStatus status)
    {
        if (status == ConsignmentStatus.Cancelled) return [];
        if (status == ConsignmentStatus.Cleared) return ["receive", "documents", "costs"];

        var next = status == ConsignmentStatus.Planned ? "in-transit" : status == ConsignmentStatus.InTransit ? "at-customs" : "cleared";
        return ["update", $"advance:{next}", "cancel", "documents", "costs"];
    }
}

public sealed class GetConsignmentQueryHandler(CatEcommerceDbContext db, IBusinessSettings settings) : IQueryHandlerAsync<GetConsignmentQuery, ConsignmentDetailViewModel?>
{
    public async Task<QueryResponse<ConsignmentDetailViewModel?>> HandleAsync(GetConsignmentQuery query)
    {
        var c = await db.Consignments.AsNoTracking()
            .Where(x => x.Id == query.ConsignmentId && x.BranchId == query.BranchId)
            .Select(x => new
            {
                x.Id, x.Reference, x.BranchId, x.OriginCountryCode, x.ShipmentReference, x.ImportPermitNumber, x.HealthCertificateNumber, x.CustomsDeclarationNumber,
                x.ExpectedArrivalOn, x.Status, x.CancellationReason, x.ClearedOn, x.Version, x.AllocatedOn, x.CostsChangedSinceAllocation, PurchaseOrderIds = x.PurchaseOrderIds,
                HsCodes = x.HsCodes.Select(h => new { h.Code, h.Description }).ToList(),
                Costs = x.CostComponents.Select(k => new { k.Kind, k.Amount, k.CurrencyCode, k.Note }).ToList(),
                Rates = x.ExchangeRates.Select(r => new { r.CurrencyCode, r.RateToBase, r.RecordedOn }).ToList(),
                Documents = x.Documents.Select(d => new
                {
                    d.Id, d.Kind, d.DocumentNumber, d.IssuedOn, d.FileName, d.StorageKey, d.ContentType, d.SizeInBytes, d.UploadedBy, d.UploadedOn, d.SupersededOn, d.SupersededBy
                }).ToList(),
                History = x.StatusHistory.Select(h => new { h.From, h.To, h.ChangedOn, h.ChangedBy, h.EffectiveOn, h.Sequence }).ToList()
            })
            .FirstOrDefaultAsync();

        if (c is null) return new QueryResponse<ConsignmentDetailViewModel?> { Result = null };

        var orders = await ConsignmentReads.PurchaseOrdersAsync(db, c.PurchaseOrderIds);

        return new QueryResponse<ConsignmentDetailViewModel?>
        {
            Result = new ConsignmentDetailViewModel(
                c.Id, c.Reference, c.BranchId, c.OriginCountryCode, c.ShipmentReference, c.ImportPermitNumber, c.HealthCertificateNumber, c.CustomsDeclarationNumber,
                c.ExpectedArrivalOn, c.Status.DisplayName, c.CancellationReason, c.ClearedOn, c.Version,
                c.HsCodes.Select(h => new HsCodeViewModel(h.Code, h.Description)).OrderBy(h => h.Code).ToList(),
                orders,
                c.Costs.Select(k => new CostComponentViewModel(k.Kind.DisplayName, InventoryViewFormat.Money(k.Amount, k.CurrencyCode), k.Note)).ToList(),
                c.Rates.Select(r => new ExchangeRateViewModel(r.CurrencyCode, r.RateToBase.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture), r.RecordedOn)).ToList(),
                c.Documents.OrderBy(d => d.UploadedOn)
                    .Select(d => ConsignmentReads.ToViewModel(d.Id, d.Kind, d.DocumentNumber, d.IssuedOn, d.FileName, d.StorageKey, d.ContentType, d.SizeInBytes, d.UploadedBy, d.UploadedOn, d.SupersededOn, d.SupersededBy))
                    .ToList(),
                await ConsignmentReads.LandedCostAsync(db, c.Id, c.AllocatedOn, c.CostsChangedSinceAllocation, settings.GetString(SettingKeys.CommerceCurrencyCode, c.BranchId)),
                c.History.OrderBy(h => h.Sequence).Select(h => new ConsignmentStatusChangeViewModel(h.From.DisplayName, h.To.DisplayName, h.ChangedOn, h.ChangedBy, h.EffectiveOn)).ToList(),
                ConsignmentReads.AllowedActions(c.Status))
        };
    }
}

/// <summary>HO-IMP-001 §12.4 / US-IMP-03 AC2 -- everything an inspector asks for, in one payload.</summary>
public sealed class GetInspectionPackQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetInspectionPackQuery, InspectionPackViewModel?>
{
    public async Task<QueryResponse<InspectionPackViewModel?>> HandleAsync(GetInspectionPackQuery query)
    {
        var c = await db.Consignments.AsNoTracking()
            .Where(x => x.Id == query.ConsignmentId && x.BranchId == query.BranchId)
            .Select(x => new
            {
                x.Id, x.Reference, x.Status, x.OriginCountryCode, x.ShipmentReference, x.ImportPermitNumber, x.HealthCertificateNumber, x.CustomsDeclarationNumber,
                x.ExpectedArrivalOn, x.ClearedOn, PurchaseOrderIds = x.PurchaseOrderIds,
                HsCodes = x.HsCodes.Select(h => new { h.Code, h.Description }).ToList(),
                Documents = x.Documents.Select(d => new
                {
                    d.Id, d.Kind, d.DocumentNumber, d.IssuedOn, d.FileName, d.StorageKey, d.ContentType, d.SizeInBytes, d.UploadedBy, d.UploadedOn, d.SupersededOn, d.SupersededBy
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (c is null) return new QueryResponse<InspectionPackViewModel?> { Result = null };

        return new QueryResponse<InspectionPackViewModel?>
        {
            Result = new InspectionPackViewModel(
                c.Id, c.Reference, c.Status.DisplayName, c.OriginCountryCode, c.ShipmentReference, c.ImportPermitNumber, c.HealthCertificateNumber,
                c.CustomsDeclarationNumber, c.ExpectedArrivalOn, c.ClearedOn,
                c.HsCodes.Select(h => new HsCodeViewModel(h.Code, h.Description)).OrderBy(h => h.Code).ToList(),
                await ConsignmentReads.PurchaseOrdersAsync(db, c.PurchaseOrderIds),
                c.Documents.OrderBy(d => d.UploadedOn)
                    .Select(d => ConsignmentReads.ToViewModel(d.Id, d.Kind, d.DocumentNumber, d.IssuedOn, d.FileName, d.StorageKey, d.ContentType, d.SizeInBytes, d.UploadedBy, d.UploadedOn, d.SupersededOn, d.SupersededBy))
                    .ToList(),
                await ConsignmentBatches.ForConsignmentAsync(db, c.Id))
        };
    }
}

/// <summary>The batches received on a consignment (provenance, FR-IMP-4): lot, expiry, quantity and the receipt each arrived on.</summary>
internal static class ConsignmentBatches
{
    public static async Task<IReadOnlyList<ConsignmentBatchViewModel>> ForConsignmentAsync(CatEcommerceDbContext db, Guid consignmentId)
    {
        var rows = await (
            from b in db.Batches.AsNoTracking()
            join g in db.GoodsReceipts.AsNoTracking() on b.GoodsReceiptId equals g.Id
            where b.ConsignmentId == consignmentId
            orderby b.ReceivedOn, b.LotNumber
            select new { b.Id, b.SkuId, b.SkuCode, b.DisplayName, b.LotNumber, b.ExpiresOn, b.ReceivedQuantity, b.StockUnit, b.Status, g.Number }).ToListAsync();

        return rows.Select(r => new ConsignmentBatchViewModel(
            r.Id, r.SkuId, r.SkuCode, r.DisplayName, r.LotNumber, r.ExpiresOn, InventoryViewFormat.Quantity(r.ReceivedQuantity), r.StockUnit, r.Status.DisplayName, r.Number)).ToList();
    }
}
