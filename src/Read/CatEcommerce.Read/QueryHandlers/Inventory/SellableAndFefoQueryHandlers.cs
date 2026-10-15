using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Inventory;

/// <summary>HO-IMP-001 §12.6 / ADR-116 -- reads <c>inventory.v_sellable_stock</c>. Every requested SKU is answered, quantity 0 when the view has no row.</summary>
public sealed class GetSellableStockQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetSellableStockQuery, IReadOnlyList<SellableStockViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<SellableStockViewModel>>> HandleAsync(GetSellableStockQuery query)
    {
        var ids = query.SkuIds.Distinct().ToList();
        var rows = ids.Count == 0
            ? []
            : await db.SellableStock.AsNoTracking().Where(s => s.BranchId == query.BranchId && ids.Contains(s.SkuId))
                .Select(s => new { s.SkuId, s.SellableQuantity, s.NextExpiresOn }).ToListAsync();

        IReadOnlyList<SellableStockViewModel> result = ids
            .Select(id => rows.FirstOrDefault(r => r.SkuId == id) is { } row
                ? new SellableStockViewModel(id, Math.Max(0m, row.SellableQuantity), row.SellableQuantity > 0m ? row.NextExpiresOn : null)
                : new SellableStockViewModel(id, 0m, null))
            .ToList();

        return new QueryResponse<IReadOnlyList<SellableStockViewModel>> { Result = result };
    }
}

public sealed class GetFefoPreviewQueryHandler(IStockLedger ledger, CatEcommerceDbContext db, ISkuDirectory skus)
    : IQueryHandlerAsync<GetFefoPreviewQuery, FefoPreviewViewModel>
{
    public async Task<QueryResponse<FefoPreviewViewModel>> HandleAsync(GetFefoPreviewQuery query)
    {
        var sku = await skus.GetAsync(query.SkuId, CancellationToken.None);
        var unit = sku?.StockUnit ?? "each";

        if (!EnumerationParsing.TryParse<MovementKind>(query.Purpose, out var purpose) || purpose is null)
            return Refused("InvalidPurpose", "The purpose must be Sale, Dispense, ClinicalUse or BoardingUse.", unit);

        var result = await ledger.PlanAsync(
            new StockDemand(query.BranchId, query.SkuId, query.Quantity, purpose, query.LocationId is { } id ? [id] : null, ExplicitLines: null, OverrideReason: null), CancellationToken.None);
        if (!result.IsSuccess) return Refused(result.ViolationCode!, result.Message!, unit);

        var plan = result.Value!;
        var locationIds = plan.Lines.Select(l => l.LocationId).Distinct().ToList();
        var codes = await db.StockLocations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).Select(l => new { Key = l.Id, Value = l.Code }).ToDictionaryAsync(x => x.Key, x => x.Value);

        return new QueryResponse<FefoPreviewViewModel>
        {
            Result = new FefoPreviewViewModel(
                plan.Lines.Select(l => new FefoPreviewLineViewModel(l.BatchId, l.LotNumber, l.ExpiresOn, l.LocationId, codes.GetValueOrDefault(l.LocationId, string.Empty), InventoryViewFormat.Quantity(l.Quantity))).ToList(),
                InventoryViewFormat.Quantity(plan.Shortfall), unit, null, null)
        };
    }

    private static QueryResponse<FefoPreviewViewModel> Refused(string code, string message, string unit) =>
        new() { Result = new FefoPreviewViewModel([], "0", unit, code, message) };
}
