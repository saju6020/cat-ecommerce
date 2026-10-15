using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Inventory;

/// <summary>What the stock-control reads need to name a batch: the SKU snapshot the batch carries (reads never join the catalogue for a name).</summary>
internal sealed record BatchLabel(Guid BatchId, string SkuCode, string DisplayName, string LotNumber, DateOnly? ExpiresOn, string StockUnit);

internal static class BatchLabels
{
    public static async Task<IReadOnlyDictionary<Guid, BatchLabel>> ForAsync(CatEcommerceDbContext db, IEnumerable<Guid> batchIds)
    {
        var ids = batchIds.Distinct().ToList();
        var rows = await db.Batches.AsNoTracking().Where(b => ids.Contains(b.Id))
            .Select(b => new BatchLabel(b.Id, b.SkuCode, b.DisplayName, b.LotNumber, b.ExpiresOn, b.StockUnit)).ToListAsync();
        return rows.ToDictionary(r => r.BatchId);
    }

    public static async Task<IReadOnlyDictionary<Guid, string>> LocationCodesAsync(CatEcommerceDbContext db, IEnumerable<Guid> locationIds)
    {
        var ids = locationIds.Distinct().ToList();
        return await db.StockLocations.AsNoTracking().Where(l => ids.Contains(l.Id)).Select(l => new { Key = l.Id, Value = l.Code }).ToDictionaryAsync(x => x.Key, x => x.Value);
    }
}

public sealed class GetStockTransfersQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStockTransfersQuery, PagedResult<StockTransferSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<StockTransferSummaryViewModel>>> HandleAsync(GetStockTransfersQuery query)
    {
        var filtered = db.StockTransfers.AsNoTracking().Where(t => t.BranchId == query.BranchId);
        var totalCount = await filtered.CountAsync();
        var rows = await filtered.OrderByDescending(t => t.TransferredOn).ThenByDescending(t => t.Number)
            .Skip(query.Skip).Take(query.Take)
            .Select(t => new { t.Id, t.Number, t.FromLocationId, t.ToLocationId, t.TransferredOn, t.TransferredBy, LineCount = t.Lines.Count, t.Reason })
            .ToListAsync();

        var codes = await BatchLabels.LocationCodesAsync(db, rows.SelectMany(r => new[] { r.FromLocationId, r.ToLocationId }));
        var items = rows.Select(r => new StockTransferSummaryViewModel(
            r.Id, r.Number, r.FromLocationId, codes.GetValueOrDefault(r.FromLocationId), r.ToLocationId, codes.GetValueOrDefault(r.ToLocationId), r.TransferredOn, r.TransferredBy,
            r.LineCount, r.Reason)).ToList();

        return new QueryResponse<PagedResult<StockTransferSummaryViewModel>>
        { Result = new PagedResult<StockTransferSummaryViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}

public sealed class GetStockTransferQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStockTransferQuery, StockTransferDetailViewModel?>
{
    public async Task<QueryResponse<StockTransferDetailViewModel?>> HandleAsync(GetStockTransferQuery query)
    {
        var transfer = await db.StockTransfers.AsNoTracking()
            .Where(t => t.Id == query.TransferId && t.BranchId == query.BranchId)
            .Select(t => new
            {
                t.Id, t.Number, t.BranchId, t.FromLocationId, t.ToLocationId, t.Reason, t.TransferredOn, t.TransferredBy,
                Lines = t.Lines.Select(l => new { l.Id, l.SkuId, l.BatchId, l.Quantity }).ToList()
            })
            .FirstOrDefaultAsync();

        if (transfer is null) return new QueryResponse<StockTransferDetailViewModel?> { Result = null };

        var codes = await BatchLabels.LocationCodesAsync(db, [transfer.FromLocationId, transfer.ToLocationId]);
        var labels = await BatchLabels.ForAsync(db, transfer.Lines.Select(l => l.BatchId));

        return new QueryResponse<StockTransferDetailViewModel?>
        {
            Result = new StockTransferDetailViewModel(
                transfer.Id, transfer.Number, transfer.BranchId, transfer.FromLocationId, codes.GetValueOrDefault(transfer.FromLocationId), transfer.ToLocationId,
                codes.GetValueOrDefault(transfer.ToLocationId), transfer.Reason, transfer.TransferredOn, transfer.TransferredBy,
                transfer.Lines.Select(l => new StockTransferLineViewModel(
                    l.Id, l.SkuId, labels.GetValueOrDefault(l.BatchId)?.SkuCode ?? string.Empty, labels.GetValueOrDefault(l.BatchId)?.DisplayName ?? string.Empty, l.BatchId,
                    labels.GetValueOrDefault(l.BatchId)?.LotNumber ?? string.Empty, InventoryViewFormat.Quantity(l.Quantity), labels.GetValueOrDefault(l.BatchId)?.StockUnit ?? "each")).ToList())
        };
    }
}

public sealed class GetStockAdjustmentsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStockAdjustmentsQuery, PagedResult<StockAdjustmentSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<StockAdjustmentSummaryViewModel>>> HandleAsync(GetStockAdjustmentsQuery query)
    {
        var filtered = db.StockAdjustments.AsNoTracking().Where(a => a.BranchId == query.BranchId);
        if (EnumerationParsing.TryParse<AdjustmentStatus>(query.Status, out var status) && status is not null) filtered = filtered.Where(a => a.Status == status);

        var totalCount = await filtered.CountAsync();
        var rows = await filtered.OrderByDescending(a => a.RequestedOn).ThenByDescending(a => a.Number)
            .Skip(query.Skip).Take(query.Take)
            .Select(a => new { a.Id, a.Number, a.LocationId, a.Reason, a.Status, a.ValueBase, a.CurrencyCode, a.RequestedBy, a.RequestedOn, LineCount = a.Lines.Count })
            .ToListAsync();

        var codes = await BatchLabels.LocationCodesAsync(db, rows.Select(r => r.LocationId));
        var items = rows.Select(r => new StockAdjustmentSummaryViewModel(
            r.Id, r.Number, r.LocationId, codes.GetValueOrDefault(r.LocationId), r.Reason.DisplayName, r.Status.DisplayName, InventoryViewFormat.Money(r.ValueBase, r.CurrencyCode),
            r.RequestedBy, r.RequestedOn, r.LineCount)).ToList();

        return new QueryResponse<PagedResult<StockAdjustmentSummaryViewModel>>
        { Result = new PagedResult<StockAdjustmentSummaryViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}

public sealed class GetStockAdjustmentQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStockAdjustmentQuery, StockAdjustmentDetailViewModel?>
{
    public async Task<QueryResponse<StockAdjustmentDetailViewModel?>> HandleAsync(GetStockAdjustmentQuery query)
    {
        var a = await db.StockAdjustments.AsNoTracking()
            .Where(x => x.Id == query.AdjustmentId && x.BranchId == query.BranchId)
            .Select(x => new
            {
                x.Id, x.Number, x.BranchId, x.LocationId, x.Reason, x.Note, x.Status, x.ValueBase, x.CurrencyCode, x.RequestedBy, x.RequestedOn, x.DecidedBy, x.DecidedOn, x.DecisionNote,
                Lines = x.Lines.Select(l => new { l.Id, l.SkuId, l.BatchId, l.QuantityDelta, l.UnitCostBase }).ToList()
            })
            .FirstOrDefaultAsync();

        if (a is null) return new QueryResponse<StockAdjustmentDetailViewModel?> { Result = null };

        var codes = await BatchLabels.LocationCodesAsync(db, [a.LocationId]);
        var labels = await BatchLabels.ForAsync(db, a.Lines.Select(l => l.BatchId));

        return new QueryResponse<StockAdjustmentDetailViewModel?>
        {
            Result = new StockAdjustmentDetailViewModel(
                a.Id, a.Number, a.BranchId, a.LocationId, codes.GetValueOrDefault(a.LocationId), a.Reason.DisplayName, a.Note, a.Status.DisplayName,
                InventoryViewFormat.Money(a.ValueBase, a.CurrencyCode), a.RequestedBy, a.RequestedOn, a.DecidedBy, a.DecidedOn, a.DecisionNote,
                a.Lines.Select(l => new StockAdjustmentLineViewModel(
                    l.Id, l.SkuId, labels.GetValueOrDefault(l.BatchId)?.SkuCode ?? string.Empty, labels.GetValueOrDefault(l.BatchId)?.DisplayName ?? string.Empty, l.BatchId,
                    labels.GetValueOrDefault(l.BatchId)?.LotNumber ?? string.Empty, InventoryViewFormat.Quantity(l.QuantityDelta), labels.GetValueOrDefault(l.BatchId)?.StockUnit ?? "each",
                    InventoryViewFormat.UnitCost(l.UnitCostBase, a.CurrencyCode))).ToList(),
                a.Status == AdjustmentStatus.PendingApproval ? ["approve", "reject", "withdraw"] : []) // approve/reject need Stock.ApproveAdjustment, withdraw the requester
        };
    }
}

public sealed class GetStockCountsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStockCountsQuery, PagedResult<StockCountSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<StockCountSummaryViewModel>>> HandleAsync(GetStockCountsQuery query)
    {
        var filtered = db.StockCounts.AsNoTracking().Where(c => c.BranchId == query.BranchId);
        if (EnumerationParsing.TryParse<StockCountStatus>(query.Status, out var status) && status is not null) filtered = filtered.Where(c => c.Status == status);
        if (query.LocationId is { } locationId) filtered = filtered.Where(c => c.LocationId == locationId);

        var totalCount = await filtered.CountAsync();
        var rows = await filtered.OrderByDescending(c => c.OpenedOn).ThenByDescending(c => c.Number)
            .Skip(query.Skip).Take(query.Take)
            .Select(c => new { c.Id, c.Number, c.LocationId, c.Scope, c.Status, LineCount = c.Lines.Count, c.VarianceValueBase, c.CurrencyCode, c.OpenedOn, c.OpenedBy })
            .ToListAsync();

        var codes = await BatchLabels.LocationCodesAsync(db, rows.Select(r => r.LocationId));
        var items = rows.Select(r => new StockCountSummaryViewModel(
            r.Id, r.Number, r.LocationId, codes.GetValueOrDefault(r.LocationId), r.Scope.DisplayName, r.Status.DisplayName, r.LineCount,
            r.VarianceValueBase is { } value && r.CurrencyCode is { } currency ? InventoryViewFormat.Money(value, currency) : null, r.OpenedOn, r.OpenedBy)).ToList();

        return new QueryResponse<PagedResult<StockCountSummaryViewModel>>
        { Result = new PagedResult<StockCountSummaryViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}

/// <summary>The count sheet. While <c>Open</c> it is BLIND: <see cref="StockCountAggregate.BookQuantitiesVisible"/> says no, so no line carries a book
/// quantity or a variance (OQ-13, U-F5).</summary>
public sealed class GetStockCountSheetQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStockCountSheetQuery, StockCountDetailViewModel?>
{
    public async Task<QueryResponse<StockCountDetailViewModel?>> HandleAsync(GetStockCountSheetQuery query)
    {
        var count = await db.StockCounts.AsNoTracking()
            .Where(c => c.Id == query.CountId && c.BranchId == query.BranchId)
            .Select(c => new
            {
                c.Id, c.Number, c.BranchId, c.LocationId, c.Scope, SkuIds = c.SkuIds, c.Status, c.OpenedOn, c.OpenedBy, c.SubmittedOn, c.VarianceValueBase, c.CurrencyCode, c.Version,
                Lines = c.Lines.Select(l => new { l.SkuId, l.BatchId, l.CountedQuantity, l.BookQuantity, l.Variance }).ToList()
            })
            .FirstOrDefaultAsync();

        if (count is null) return new QueryResponse<StockCountDetailViewModel?> { Result = null };

        var visible = StockCountAggregate.BookQuantitiesVisible(count.Status);
        var codes = await BatchLabels.LocationCodesAsync(db, [count.LocationId]);
        var labels = await BatchLabels.ForAsync(db, count.Lines.Select(l => l.BatchId));

        var lines = count.Lines
            .Select(l => (Line: l, Label: labels.GetValueOrDefault(l.BatchId)))
            .OrderBy(x => x.Label?.SkuCode).ThenBy(x => x.Label?.LotNumber)
            .Select(x => new StockCountLineViewModel(
                x.Line.SkuId, x.Label?.SkuCode ?? string.Empty, x.Label?.DisplayName ?? string.Empty, x.Line.BatchId, x.Label?.LotNumber ?? string.Empty, x.Label?.ExpiresOn,
                x.Label?.StockUnit ?? "each", InventoryViewFormat.Quantity(x.Line.CountedQuantity),
                visible ? InventoryViewFormat.Quantity(x.Line.BookQuantity) : null, visible ? InventoryViewFormat.Quantity(x.Line.Variance) : null)).ToList();

        return new QueryResponse<StockCountDetailViewModel?>
        {
            Result = new StockCountDetailViewModel(
                count.Id, count.Number, count.BranchId, count.LocationId, codes.GetValueOrDefault(count.LocationId), count.Scope.DisplayName, count.SkuIds, count.Status.DisplayName,
                count.OpenedOn, count.OpenedBy, count.SubmittedOn,
                visible && count.VarianceValueBase is { } value && count.CurrencyCode is { } currency ? InventoryViewFormat.Money(value, currency) : null, !visible, lines,
                AllowedActionsFor(count.Status), count.Version)
        };
    }

    private static IReadOnlyList<string> AllowedActionsFor(StockCountStatus status) =>
        status == StockCountStatus.Open ? ["record-lines", "submit", "cancel"]
        : status == StockCountStatus.Submitted ? ["commit", "cancel"]
        : status == StockCountStatus.AwaitingApproval ? ["approve", "reject"]
        : [];
}

/// <summary>US-IMP-14 AC1 -- the variance report: book, counted, variance and value per line, and the total -- produced after submission, while NO
/// ledger entry exists yet. <c>null</c> until the count has been submitted.</summary>
public sealed class GetStockCountVarianceQueryHandler(CatEcommerceDbContext db, IBusinessSettings settings)
    : IQueryHandlerAsync<GetStockCountVarianceQuery, StockCountVarianceViewModel?>
{
    public async Task<QueryResponse<StockCountVarianceViewModel?>> HandleAsync(GetStockCountVarianceQuery query)
    {
        var count = await db.StockCounts.AsNoTracking()
            .Where(c => c.Id == query.CountId && c.BranchId == query.BranchId)
            .Select(c => new
            {
                c.Id, c.Number, c.Status, c.VarianceValueBase, c.CurrencyCode,
                Lines = c.Lines.Select(l => new { l.SkuId, l.BatchId, l.CountedQuantity, l.BookQuantity, l.Variance, l.UnitCostBase }).ToList()
            })
            .FirstOrDefaultAsync();

        if (count is null || !StockCountAggregate.BookQuantitiesVisible(count.Status) || count.CurrencyCode is null)
            return new QueryResponse<StockCountVarianceViewModel?> { Result = null };

        var labels = await BatchLabels.ForAsync(db, count.Lines.Select(l => l.BatchId));
        var currency = count.CurrencyCode;

        var lines = count.Lines
            .Select(l => (Line: l, Label: labels.GetValueOrDefault(l.BatchId)))
            .OrderBy(x => x.Label?.SkuCode).ThenBy(x => x.Label?.LotNumber)
            .Select(x => new StockCountVarianceLineViewModel(
                x.Line.SkuId, x.Label?.SkuCode ?? string.Empty, x.Label?.DisplayName ?? string.Empty, x.Line.BatchId, x.Label?.LotNumber ?? string.Empty, x.Label?.StockUnit ?? "each",
                InventoryViewFormat.Quantity(x.Line.BookQuantity ?? 0m), InventoryViewFormat.Quantity(x.Line.CountedQuantity), InventoryViewFormat.Quantity(x.Line.Variance ?? 0m),
                InventoryViewFormat.Money((x.Line.Variance ?? 0m) * x.Line.UnitCostBase, currency))).ToList();

        var total = count.VarianceValueBase ?? 0m;
        var threshold = settings.GetDecimal(Shared.Common.Constants.SettingKeys.InventoryAdjustmentApprovalThreshold, query.BranchId);

        return new QueryResponse<StockCountVarianceViewModel?>
        {
            Result = new StockCountVarianceViewModel(
                count.Id, count.Number, count.Status.DisplayName, InventoryViewFormat.Money(total, currency), lines.Count(l => l.Variance != "0"), total > threshold, lines)
        };
    }
}
