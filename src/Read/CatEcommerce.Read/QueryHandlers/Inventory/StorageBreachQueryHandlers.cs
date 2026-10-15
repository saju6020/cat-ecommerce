using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Inventory;

public sealed class GetStorageBreachesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStorageBreachesQuery, PagedResult<StorageBreachSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<StorageBreachSummaryViewModel>>> HandleAsync(GetStorageBreachesQuery query)
    {
        var reports = db.StorageBreachReports.AsNoTracking().Where(r => r.BranchId == query.BranchId);
        if (EnumerationParsing.TryParse<StorageBreachStatus>(query.Status, out var status) && status is not null) reports = reports.Where(r => r.Status == status);

        var total = await reports.CountAsync();
        var page = await reports.OrderByDescending(r => r.ReportedOn).ThenBy(r => r.Id).Skip(query.Skip).Take(query.Take)
            .Select(r => new { r.Id, r.LocationId, r.ObservedOn, r.Condition, r.Status, Count = r.AffectedBatchIds.Count, r.ReportedBy, r.ReportedOn }).ToListAsync();

        var locationIds = page.Select(r => r.LocationId).Distinct().ToList();
        var codes = await db.StockLocations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).Select(l => new { Key = l.Id, Value = l.Code }).ToDictionaryAsync(x => x.Key, x => x.Value);

        var items = page.Select(r => new StorageBreachSummaryViewModel(
            r.Id, r.LocationId, codes.GetValueOrDefault(r.LocationId), r.ObservedOn, r.Condition, r.Status.DisplayName, r.Count, r.ReportedBy, r.ReportedOn)).ToList();

        return new QueryResponse<PagedResult<StorageBreachSummaryViewModel>>
        { Result = new PagedResult<StorageBreachSummaryViewModel>(items, query.PageNumber, query.PageSize, total) };
    }
}

public sealed class GetStorageBreachQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStorageBreachQuery, StorageBreachDetailViewModel?>
{
    public async Task<QueryResponse<StorageBreachDetailViewModel?>> HandleAsync(GetStorageBreachQuery query)
    {
        var r = await db.StorageBreachReports.AsNoTracking().Where(x => x.Id == query.BreachId && x.BranchId == query.BranchId)
            .Select(x => new { x.Id, x.LocationId, x.ObservedOn, x.Condition, x.Status, x.ReportedBy, x.ReportedOn, x.TemperatureC, x.DurationMinutes, x.AffectedBatchIds, x.QuarantinedOn, x.QuarantinedBy, x.ClosedOn, x.ClosedBy, x.ClosingNote })
            .FirstOrDefaultAsync();
        if (r is null) return new QueryResponse<StorageBreachDetailViewModel?> { Result = null };

        var code = await db.StockLocations.AsNoTracking().Where(l => l.Id == r.LocationId).Select(l => l.Code).FirstOrDefaultAsync();
        var batchIds = r.AffectedBatchIds.ToList();
        var batches = await db.Batches.AsNoTracking().Where(b => batchIds.Contains(b.Id)).Select(b => new { b.Id, b.SkuCode, b.DisplayName, b.LotNumber }).ToListAsync();
        var held = (await db.StockPositions.AsNoTracking().Where(p => p.LocationId == r.LocationId && batchIds.Contains(p.BatchId)).ToListAsync()).ToDictionary(p => p.BatchId, p => p.OnHand);

        var affected = batchIds.Select(id =>
        {
            var b = batches.FirstOrDefault(x => x.Id == id);
            return new StorageBreachBatchViewModel(id, b?.SkuCode, b?.DisplayName, b?.LotNumber, InventoryViewFormat.Quantity(held.GetValueOrDefault(id)));
        }).ToList();

        var summary = new StorageBreachSummaryViewModel(r.Id, r.LocationId, code, r.ObservedOn, r.Condition, r.Status.DisplayName, batchIds.Count, r.ReportedBy, r.ReportedOn);
        IReadOnlyList<string> actions = r.Status == StorageBreachStatus.Open ? ["quarantine", "close"] : r.Status == StorageBreachStatus.Quarantined ? ["close"] : [];

        return new QueryResponse<StorageBreachDetailViewModel?>
        { Result = new StorageBreachDetailViewModel(summary, r.TemperatureC, r.DurationMinutes, affected, r.QuarantinedOn, r.QuarantinedBy, r.ClosedOn, r.ClosedBy, r.ClosingNote, actions) };
    }
}
