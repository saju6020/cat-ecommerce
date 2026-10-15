using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Inventory;

/// <summary>HO-IMP-001 §12.4 -- goods receipts of the active branch, paged; filtering, sorting and paging in SQL (UI-4).</summary>
public sealed class GetGoodsReceiptsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetGoodsReceiptsQuery, PagedResult<GoodsReceiptSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<GoodsReceiptSummaryViewModel>>> HandleAsync(GetGoodsReceiptsQuery query)
    {
        var filtered = db.GoodsReceipts.AsNoTracking().Where(g => g.BranchId == query.BranchId);

        if (query.From is { } from) filtered = filtered.Where(g => g.ReceivedOn >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (query.To is { } to) filtered = filtered.Where(g => g.ReceivedOn < to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (query.SupplierId is { } supplierId) filtered = filtered.Where(g => g.SupplierId == supplierId);
        if (query.PurchaseOrderId is { } orderId) filtered = filtered.Where(g => g.PurchaseOrderId == orderId);

        var totalCount = await filtered.CountAsync();
        var rows = await filtered.OrderByDescending(g => g.ReceivedOn).ThenByDescending(g => g.Number)
            .Skip(query.Skip).Take(query.Take)
            .Select(g => new
            {
                g.Id, g.Number, g.ReceivedOn, g.SupplierId, g.PurchaseOrderId, g.ConsignmentId, g.DeliveryReference, g.ReceivedBy,
                LineCount = g.Lines.Count, BatchCount = g.Lines.Count(l => l.BatchId != null)
            })
            .ToListAsync();

        var supplierIds = rows.Where(r => r.SupplierId != null).Select(r => r.SupplierId!.Value).Distinct().ToList();
        var orderIds = rows.Where(r => r.PurchaseOrderId != null).Select(r => r.PurchaseOrderId!.Value).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).Select(s => new { Key = s.Id, Value = s.Name }).ToDictionaryAsync(x => x.Key, x => x.Value);
        var orders = await db.PurchaseOrders.AsNoTracking().Where(p => orderIds.Contains(p.Id)).Select(p => new { Key = p.Id, Value = p.Number }).ToDictionaryAsync(x => x.Key, x => x.Value);

        var items = rows.Select(r => new GoodsReceiptSummaryViewModel(
            r.Id, r.Number, r.ReceivedOn, r.SupplierId, r.SupplierId is { } s ? suppliers.GetValueOrDefault(s) : null, r.PurchaseOrderId,
            r.PurchaseOrderId is { } o ? orders.GetValueOrDefault(o) : null, r.ConsignmentId, r.DeliveryReference, r.LineCount, r.BatchCount, r.ReceivedBy)).ToList();

        return new QueryResponse<PagedResult<GoodsReceiptSummaryViewModel>>
        { Result = new PagedResult<GoodsReceiptSummaryViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}

/// <summary>A receipt with its lines, the batch each became, the location, and each product's <c>storage</c> (US-IMP-16 AC1).</summary>
public sealed class GetGoodsReceiptQueryHandler(CatEcommerceDbContext db, ISkuDirectory skus)
    : IQueryHandlerAsync<GetGoodsReceiptQuery, GoodsReceiptDetailViewModel?>
{
    public async Task<QueryResponse<GoodsReceiptDetailViewModel?>> HandleAsync(GetGoodsReceiptQuery query)
    {
        var receipt = await db.GoodsReceipts.AsNoTracking()
            .Where(g => g.Id == query.GoodsReceiptId && g.BranchId == query.BranchId)
            .Select(g => new
            {
                g.Id, g.Number, g.BranchId, g.ReceivedOn, g.ReceivedBy, g.SupplierId, g.PurchaseOrderId, g.ConsignmentId, g.DeliveryReference,
                Lines = g.Lines.Select(l => new
                {
                    l.Id, l.SkuId, l.SkuCode, l.DisplayName, l.StockUnit, l.LotNumber, l.ManufacturedOn, l.ExpiresOn, l.QuantityAccepted, l.QuantityRejected,
                    l.RejectionReason, l.RejectionNote, l.LocationId, l.HoldForInspection, l.UnitCostAmount, l.UnitCostCurrency, l.BatchId
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (receipt is null) return new QueryResponse<GoodsReceiptDetailViewModel?> { Result = null };

        var supplierName = receipt.SupplierId is { } supplierId
            ? await db.Suppliers.AsNoTracking().Where(s => s.Id == supplierId).Select(s => s.Name).FirstOrDefaultAsync() : null;
        var orderNumber = receipt.PurchaseOrderId is { } orderId
            ? await db.PurchaseOrders.AsNoTracking().Where(p => p.Id == orderId).Select(p => p.Number).FirstOrDefaultAsync() : null;
        var consignmentReference = receipt.ConsignmentId is { } consignmentId
            ? await db.Consignments.AsNoTracking().Where(c => c.Id == consignmentId).Select(c => c.Reference).FirstOrDefaultAsync() : null;

        var locationIds = receipt.Lines.Select(l => l.LocationId).Distinct().ToList();
        var locationCodes = await db.StockLocations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).Select(l => new { Key = l.Id, Value = l.Code }).ToDictionaryAsync(x => x.Key, x => x.Value);
        var batchIds = receipt.Lines.Where(l => l.BatchId != null).Select(l => l.BatchId!.Value).ToList();
        var batchStatuses = await db.Batches.AsNoTracking().Where(b => batchIds.Contains(b.Id)).Select(b => new { b.Id, b.Status }).ToDictionaryAsync(b => b.Id, b => b.Status.DisplayName);

        var profiles = (await skus.GetManyAsync(receipt.Lines.Select(l => l.SkuId).Distinct().ToList(), CancellationToken.None)).ToDictionary(s => s.SkuId);
        var storage = await StorageLookup.ForProductsAsync(db, profiles.Values.Select(p => p.ProductId).Distinct().ToList());

        var lines = receipt.Lines.Select(l => new GoodsReceiptLineViewModel(
            l.Id, l.SkuId, l.SkuCode, l.DisplayName, l.StockUnit, l.LotNumber, l.ManufacturedOn, l.ExpiresOn,
            InventoryViewFormat.Quantity(l.QuantityAccepted), InventoryViewFormat.Quantity(l.QuantityRejected), l.RejectionReason?.DisplayName, l.RejectionNote,
            l.LocationId, locationCodes.GetValueOrDefault(l.LocationId), l.HoldForInspection,
            query.IncludeCosts && l.UnitCostAmount is { } amount && l.UnitCostCurrency is { } currency ? InventoryViewFormat.UnitCost(amount, currency) : null,
            l.BatchId, l.BatchId is { } b ? batchStatuses.GetValueOrDefault(b) : null,
            profiles.TryGetValue(l.SkuId, out var profile) ? storage.GetValueOrDefault(profile.ProductId) : null)).ToList();

        return new QueryResponse<GoodsReceiptDetailViewModel?>
        {
            Result = new GoodsReceiptDetailViewModel(
                receipt.Id, receipt.Number, receipt.BranchId, receipt.ReceivedOn, receipt.ReceivedBy, receipt.SupplierId, supplierName, receipt.PurchaseOrderId, orderNumber,
                receipt.ConsignmentId, consignmentReference, receipt.DeliveryReference, lines)
        };
    }
}

/// <summary>HO-IMP-001 §12.4 -- one row per SKU with stock in the branch. The positions are MATERIALISED and the buckets computed in C# with
/// <see cref="BatchExpiryRules"/> against the branch's today: the read never has a second expiry rule of its own in LINQ (ADR-101).</summary>
public sealed class GetStockSummaryQueryHandler(CatEcommerceDbContext db, ISkuDirectory skus, IInventoryClock clock, IBusinessSettings settings)
    : IQueryHandlerAsync<GetStockSummaryQuery, PagedResult<StockSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<StockSummaryViewModel>>> HandleAsync(GetStockSummaryQuery query)
    {
        var today = await clock.LocalTodayAsync(query.BranchId, CancellationToken.None);

        IReadOnlyList<Guid>? searchSkuIds = null;
        if (!string.IsNullOrWhiteSpace(query.Search))
            searchSkuIds = (await skus.SearchAsync(query.Search, 500, CancellationToken.None)).Select(s => s.SkuId).ToList();

        var positions = from p in db.StockPositions.AsNoTracking()
                        join b in db.Batches.AsNoTracking() on p.BatchId equals b.Id
                        join l in db.StockLocations.AsNoTracking() on p.LocationId equals l.Id
                        where l.BranchId == query.BranchId && (p.OnHand > 0m || p.Reserved > 0m)
                        select new { p.SkuId, p.OnHand, p.Reserved, b.Status, b.ExpiresOn, b.StockClass, LocationId = l.Id, LocationKind = l.Kind };

        if (query.LocationId is { } locationId) positions = positions.Where(p => p.LocationId == locationId);
        if (searchSkuIds is not null) positions = positions.Where(p => searchSkuIds.Contains(p.SkuId));
        if (EnumerationParsing.TryParse<StockClass>(query.Class, out var stockClass) && stockClass is not null) positions = positions.Where(p => p.StockClass == stockClass);

        var rows = await positions.ToListAsync();
        var summaries = rows.GroupBy(r => r.SkuId).Select(g =>
        {
            decimal onHand = 0, reserved = 0, usable = 0, sellable = 0, expired = 0, held = 0;
            DateOnly? next = null;
            foreach (var r in g)
            {
                onHand += r.OnHand;
                reserved += r.Reserved;

                if (r.Status == BatchStatus.Quarantined || r.Status == BatchStatus.Recalled || r.LocationKind == StockLocationKind.Quarantine) { held += r.OnHand; continue; }
                if (BatchExpiryRules.IsExpired(r.ExpiresOn, today)) { expired += r.OnHand; continue; }
                if (r.Status != BatchStatus.Available) continue;

                var free = r.OnHand - r.Reserved;
                usable += free;
                if (StockLocationRules.CountsTowardRetailSellable(r.LocationKind)) sellable += free;
                if (free > 0m && r.ExpiresOn is { } expires && (next is null || expires < next)) next = expires;
            }

            return new { SkuId = g.Key, OnHand = onHand, Reserved = reserved, Usable = usable, Sellable = sellable, Expired = expired, Held = held, Next = next };
        }).ToList();

        if (query.BelowReorderPoint)
        {
            // US-IMP-12 -- a SKU with any pair (at the filtered location) whose usable stock is below its effective reorder point; one with nothing left at all is the lowest of them.
            var low = (await ReorderReads.PairsAsync(db, clock, settings, query.BranchId, query.LocationId)).Where(p => p.Point > 0m && p.Usable < p.Point).Select(p => p.SkuId).ToHashSet();
            var present = summaries.Select(s => s.SkuId).ToHashSet();
            summaries = summaries.Where(s => low.Contains(s.SkuId))
                .Concat(low.Where(id => !present.Contains(id)).Select(id => new { SkuId = id, OnHand = 0m, Reserved = 0m, Usable = 0m, Sellable = 0m, Expired = 0m, Held = 0m, Next = (DateOnly?)null })).ToList();
        }

        var profiles = (await skus.GetManyAsync(summaries.Select(s => s.SkuId).ToList(), CancellationToken.None)).ToDictionary(s => s.SkuId);
        var ordered = summaries.Where(s => profiles.ContainsKey(s.SkuId)).OrderBy(s => profiles[s.SkuId].DisplayName).ThenBy(s => profiles[s.SkuId].SkuCode).ToList();

        var page = ordered.Skip(query.Skip).Take(query.Take).ToList();
        var storage = await StorageLookup.ForProductsAsync(db, page.Select(s => profiles[s.SkuId].ProductId).Distinct().ToList());

        var items = page.Select(s =>
        {
            var p = profiles[s.SkuId];
            return new StockSummaryViewModel(
                s.SkuId, p.SkuCode, p.DisplayName, p.StockUnit, p.StockClass.DisplayName, InventoryViewFormat.Quantity(s.OnHand), InventoryViewFormat.Quantity(s.Reserved),
                InventoryViewFormat.Quantity(s.Usable), InventoryViewFormat.Quantity(s.Sellable), InventoryViewFormat.Quantity(s.Expired), InventoryViewFormat.Quantity(s.Held),
                s.Next, storage.GetValueOrDefault(p.ProductId));
        }).ToList();

        return new QueryResponse<PagedResult<StockSummaryViewModel>>
        { Result = new PagedResult<StockSummaryViewModel>(items, query.PageNumber, query.PageSize, ordered.Count) };
    }
}

/// <summary>US-IMP-06 -- the batch x location matrix of one SKU, with every batch's derived expiry state.</summary>
public sealed class GetSkuStockMatrixQueryHandler(CatEcommerceDbContext db, ISkuDirectory skus, IInventoryClock clock)
    : IQueryHandlerAsync<GetSkuStockMatrixQuery, SkuStockMatrixViewModel?>
{
    public async Task<QueryResponse<SkuStockMatrixViewModel?>> HandleAsync(GetSkuStockMatrixQuery query)
    {
        var profile = await skus.GetAsync(query.SkuId, CancellationToken.None);
        if (profile is null) return new QueryResponse<SkuStockMatrixViewModel?> { Result = null };

        var zone = await clock.TimeZoneIdAsync(query.BranchId, CancellationToken.None);
        var today = await clock.LocalTodayAsync(query.BranchId, CancellationToken.None);

        var batches = await db.Batches.AsNoTracking()
            .Where(b => b.SkuId == query.SkuId && b.BranchId == query.BranchId)
            .Select(b => new { b.Id, b.LotNumber, b.ExpiresOn, b.Status, b.ReceivedOn })
            .ToListAsync();

        var batchIds = batches.Select(b => b.Id).ToList();
        var positions = await (
            from p in db.StockPositions.AsNoTracking()
            join l in db.StockLocations.AsNoTracking() on p.LocationId equals l.Id
            where batchIds.Contains(p.BatchId) && l.BranchId == query.BranchId && (p.OnHand > 0m || p.Reserved > 0m)
            select new { p.BatchId, LocationId = l.Id, l.Code, l.Kind, p.OnHand, p.Reserved }).ToListAsync();

        var storage = await StorageLookup.ForProductsAsync(db, [profile.ProductId]);

        var matrix = batches.OrderBy(b => b.ExpiresOn == null).ThenBy(b => b.ExpiresOn).ThenBy(b => b.ReceivedOn)
            .Select(b => new StockMatrixBatchViewModel(
                b.Id, b.LotNumber, b.ExpiresOn, BatchExpiryRules.IsExpired(b.ExpiresOn, today), BatchExpiryRules.DaysToExpiry(b.ExpiresOn, today), b.Status.DisplayName,
                positions.Where(p => p.BatchId == b.Id).OrderBy(p => p.Code)
                    .Select(p => new StockLocationQuantityViewModel(p.LocationId, p.Code, p.Kind.DisplayName, InventoryViewFormat.Quantity(p.OnHand), InventoryViewFormat.Quantity(p.Reserved)))
                    .ToList()))
            .Where(b => b.Locations.Count > 0 || b.Status != BatchStatus.WrittenOff.DisplayName)
            .ToList();

        return new QueryResponse<SkuStockMatrixViewModel?>
        { Result = new SkuStockMatrixViewModel(profile.SkuId, profile.SkuCode, profile.DisplayName, profile.StockUnit, storage.GetValueOrDefault(profile.ProductId), zone, matrix) };
    }
}

/// <summary>HO-IMP-001 §12.4 -- batches of the active branch, paged; every row carries the derived expiry state.</summary>
public sealed class GetBatchesQueryHandler(CatEcommerceDbContext db, IInventoryClock clock)
    : IQueryHandlerAsync<GetBatchesQuery, PagedResult<BatchSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<BatchSummaryViewModel>>> HandleAsync(GetBatchesQuery query)
    {
        var zone = await clock.TimeZoneIdAsync(query.BranchId, CancellationToken.None);
        var today = await clock.LocalTodayAsync(query.BranchId, CancellationToken.None);

        var filtered = db.Batches.AsNoTracking().Where(b => b.BranchId == query.BranchId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(b => b.LotNumber.ToLower().Contains(term) || b.SkuCode.ToLower().Contains(term) || b.DisplayName.ToLower().Contains(term));
        }

        if (query.SkuId is { } skuId) filtered = filtered.Where(b => b.SkuId == skuId);
        if (query.ConsignmentId is { } consignmentId) filtered = filtered.Where(b => b.ConsignmentId == consignmentId);
        if (EnumerationParsing.TryParse<BatchStatus>(query.Status, out var status) && status is not null) filtered = filtered.Where(b => b.Status == status);
        if (query.ExpiringWithinDays is { } days)
        {
            var bound = today.AddDays(days);
            filtered = filtered.Where(b => b.ExpiresOn != null && b.ExpiresOn <= bound);
        }

        var totalCount = await filtered.CountAsync();
        var rows = await filtered.OrderBy(b => b.ExpiresOn == null).ThenBy(b => b.ExpiresOn).ThenBy(b => b.ReceivedOn).ThenBy(b => b.Id)
            .Skip(query.Skip).Take(query.Take)
            .Select(b => new { b.Id, b.SkuId, b.SkuCode, b.DisplayName, b.LotNumber, b.ExpiresOn, b.Status, b.ReceivedQuantity, b.StockUnit, b.ConsignmentId })
            .ToListAsync();

        var ids = rows.Select(r => r.Id).ToList();
        var onHand = await db.StockPositions.AsNoTracking().Where(p => ids.Contains(p.BatchId))
            .GroupBy(p => p.BatchId).Select(g => new { BatchId = g.Key, Quantity = g.Sum(p => p.OnHand) })
            .ToDictionaryAsync(g => g.BatchId, g => g.Quantity);

        var items = rows.Select(r => new BatchSummaryViewModel(
            r.Id, r.SkuId, r.SkuCode, r.DisplayName, r.LotNumber, r.ExpiresOn, BatchExpiryRules.IsExpired(r.ExpiresOn, today), BatchExpiryRules.DaysToExpiry(r.ExpiresOn, today),
            zone, r.Status.DisplayName, InventoryViewFormat.Quantity(r.ReceivedQuantity), InventoryViewFormat.Quantity(onHand.GetValueOrDefault(r.Id)), r.StockUnit, r.ConsignmentId)).ToList();

        return new QueryResponse<PagedResult<BatchSummaryViewModel>>
        { Result = new PagedResult<BatchSummaryViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}

/// <summary>A batch in full: provenance, costs (only when <c>IncludeCosts</c>), positions, history and the actions its status allows now
/// (<see cref="BatchTransitionRules"/>).</summary>
public sealed class GetBatchQueryHandler(CatEcommerceDbContext db, ISkuDirectory skus, IInventoryClock clock, IStockPositionReader positionReader, IBusinessSettings settings)
    : IQueryHandlerAsync<GetBatchQuery, BatchDetailViewModel?>
{
    public async Task<QueryResponse<BatchDetailViewModel?>> HandleAsync(GetBatchQuery query)
    {
        var batch = await db.Batches.AsNoTracking()
            .Where(b => b.Id == query.BatchId && b.BranchId == query.BranchId)
            .Select(b => new
            {
                b.Id, b.SkuId, b.SkuCode, b.DisplayName, b.StockUnit, b.StockClass, b.BranchId, b.LotNumber, b.ManufacturedOn, b.ExpiresOn, b.Status, b.StatusReason,
                b.ReceivedOn, b.ReceivedQuantity, b.GoodsReceiptId, b.ConsignmentId, b.SupplierId, b.OriginCountryCode, b.PurchaseUnitCostAmount,
                b.PurchaseUnitCostCurrency, b.Version,
                History = b.StatusHistory.Select(h => new { h.From, h.To, h.Reason, h.ChangedOn, h.ChangedBy, h.Sequence }).ToList()
            })
            .FirstOrDefaultAsync();

        if (batch is null) return new QueryResponse<BatchDetailViewModel?> { Result = null };

        var zone = await clock.TimeZoneIdAsync(batch.BranchId, CancellationToken.None);
        var today = await clock.LocalTodayAsync(batch.BranchId, CancellationToken.None);

        var receiptNumber = await db.GoodsReceipts.AsNoTracking().Where(g => g.Id == batch.GoodsReceiptId).Select(g => g.Number).FirstOrDefaultAsync();
        var consignmentReference = batch.ConsignmentId is { } consignmentId
            ? await db.Consignments.AsNoTracking().Where(c => c.Id == consignmentId).Select(c => c.Reference).FirstOrDefaultAsync() : null;

        var positions = await (
            from p in db.StockPositions.AsNoTracking()
            join l in db.StockLocations.AsNoTracking() on p.LocationId equals l.Id
            where p.BatchId == batch.Id && (p.OnHand > 0m || p.Reserved > 0m)
            select new { LocationId = l.Id, l.Code, l.Kind, p.OnHand, p.Reserved }).ToListAsync();

        var profile = await skus.GetAsync(batch.SkuId, CancellationToken.None);
        var storage = profile is null ? null : (await StorageLookup.ForProductsAsync(db, [profile.ProductId])).GetValueOrDefault(profile.ProductId);

        MoneyViewModel? purchase = null, unitCostBase = null;
        string? basis = null;
        if (query.IncludeCosts)
        {
            var baseCurrency = settings.GetString(SettingKeys.CommerceCurrencyCode, batch.BranchId);
            purchase = InventoryViewFormat.UnitCost(batch.PurchaseUnitCostAmount, batch.PurchaseUnitCostCurrency);
            var costs = await positionReader.GetUnitCostsAsync([batch.Id], CancellationToken.None);
            if (costs.TryGetValue(batch.Id, out var cost))
            {
                unitCostBase = InventoryViewFormat.UnitCost(cost.UnitCostBase, baseCurrency);
                basis = cost.Basis;
            }
        }

        return new QueryResponse<BatchDetailViewModel?>
        {
            Result = new BatchDetailViewModel(
                batch.Id, batch.SkuId, batch.SkuCode, batch.DisplayName, batch.StockUnit, batch.StockClass.DisplayName, batch.BranchId, batch.LotNumber,
                batch.ManufacturedOn, batch.ExpiresOn, BatchExpiryRules.IsExpired(batch.ExpiresOn, today), BatchExpiryRules.DaysToExpiry(batch.ExpiresOn, today), zone,
                batch.Status.DisplayName, batch.StatusReason, batch.ReceivedOn, InventoryViewFormat.Quantity(batch.ReceivedQuantity), batch.GoodsReceiptId, receiptNumber,
                batch.ConsignmentId, consignmentReference, batch.SupplierId, batch.OriginCountryCode, purchase, unitCostBase, basis,
                positions.OrderBy(p => p.Code).Select(p => new BatchPositionViewModel(p.LocationId, p.Code, p.Kind.DisplayName, InventoryViewFormat.Quantity(p.OnHand), InventoryViewFormat.Quantity(p.Reserved))).ToList(),
                await BatchRecallReads.ForBatchAsync(db, batch.Id),
                batch.History.OrderBy(h => h.Sequence).Select(h => new BatchStatusChangeViewModel(h.From.DisplayName, h.To.DisplayName, h.Reason, h.ChangedOn, h.ChangedBy)).ToList(),
                BatchTransitionRules.AllowedActions(batch.Status), storage, batch.Version)
        };
    }
}

/// <summary>The owned recall record of a batch, when it has one.</summary>
internal static class BatchRecallReads
{
    public static async Task<BatchRecallViewModel?> ForBatchAsync(CatEcommerceDbContext db, Guid batchId)
    {
        var r = await db.Batches.AsNoTracking().Where(b => b.Id == batchId && b.CurrentRecall != null).Select(b => b.CurrentRecall).FirstOrDefaultAsync();
        return r is null ? null : new BatchRecallViewModel(
            r.RecallNumber, r.Reason, r.CustomerAdvice, r.DeclaredOn, r.DeclaredBy, r.NotifiedOn, r.NotifiedBy, r.NotifiedRecipientCount, r.NotificationRuns, r.ClosedOn, r.ClosingNote);
    }
}

/// <summary>HO-IMP-001 §12.4 / US-IMP-15 AC1 -- the ledger search: SKU, batch, delta, location, reference and user on every row. The range is
/// REQUIRED (the controller refuses a missing or over-long one) and rows carry NO customer or cat (those are in the recall trace only).</summary>
public sealed class GetLedgerQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetLedgerQuery, PagedResult<LedgerEntryViewModel>>
{
    public async Task<QueryResponse<PagedResult<LedgerEntryViewModel>>> HandleAsync(GetLedgerQuery query)
    {
        var from = query.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = query.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var filtered = db.StockLedgerEntries.AsNoTracking()
            .Where(e => e.BranchId == query.BranchId && e.OccurredOn >= from && e.OccurredOn < to);

        if (query.SkuId is { } skuId) filtered = filtered.Where(e => e.SkuId == skuId);
        if (query.BatchId is { } batchId) filtered = filtered.Where(e => e.BatchId == batchId);
        if (query.LocationId is { } locationId) filtered = filtered.Where(e => e.LocationId == locationId);
        if (query.ReferenceId is { } referenceId) filtered = filtered.Where(e => e.ReferenceId == referenceId);
        if (EnumerationParsing.TryParse<MovementKind>(query.Kind, out var kind) && kind is not null) filtered = filtered.Where(e => e.Kind == kind);
        if (EnumerationParsing.TryParse<StockReferenceKind>(query.ReferenceKind, out var referenceKind) && referenceKind is not null) filtered = filtered.Where(e => e.ReferenceKind == referenceKind);

        var totalCount = await filtered.CountAsync();
        var rows = await filtered.OrderByDescending(e => e.Sequence)
            .Skip(query.Skip).Take(query.Take)
            .Select(e => new
            {
                e.Sequence, e.OccurredOn, e.Kind, e.QuantityDelta, e.LocationId, e.BatchId, e.SkuId, e.ReferenceKind, e.ReferenceId, e.ReferenceLabel,
                e.FefoOverridden, e.OverrideReason, e.PerformedBy, e.Note
            })
            .ToListAsync();

        var locationIds = rows.Select(r => r.LocationId).Distinct().ToList();
        var batchIds = rows.Select(r => r.BatchId).Distinct().ToList();
        var performerIds = rows.Select(r => r.PerformedBy).Distinct().ToList();

        var locations = await db.StockLocations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).Select(l => new { Key = l.Id, Value = l.Code }).ToDictionaryAsync(x => x.Key, x => x.Value);
        var batches = await db.Batches.AsNoTracking().Where(b => batchIds.Contains(b.Id)).Select(b => new { b.Id, b.LotNumber, b.SkuCode, b.StockUnit }).ToDictionaryAsync(b => b.Id);
        var performers = await db.StaffAccounts.AsNoTracking().Where(s => performerIds.Contains(s.Id)).Select(s => new { Key = s.Id, Value = s.FullName }).ToDictionaryAsync(x => x.Key, x => x.Value);

        var items = rows.Select(r =>
        {
            var batch = batches.GetValueOrDefault(r.BatchId);
            return new LedgerEntryViewModel(
                r.Sequence, r.OccurredOn, r.Kind.DisplayName, InventoryViewFormat.Quantity(r.QuantityDelta), batch?.StockUnit ?? string.Empty, r.LocationId,
                locations.GetValueOrDefault(r.LocationId), r.BatchId, batch?.LotNumber, r.SkuId, batch?.SkuCode, r.ReferenceKind.DisplayName, r.ReferenceId, r.ReferenceLabel,
                r.FefoOverridden, r.OverrideReason, r.PerformedBy, performers.GetValueOrDefault(r.PerformedBy), r.Note);
        }).ToList();

        return new QueryResponse<PagedResult<LedgerEntryViewModel>>
        { Result = new PagedResult<LedgerEntryViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}
