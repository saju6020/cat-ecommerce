using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Inventory;

/// <summary>One (SKU, location) pair as the low-stock and reorder reads see it: its usable stock and its effective reorder point.</summary>
internal sealed record ReorderPair(
    Guid BranchId, Guid SkuId, string SkuCode, string DisplayName, string StockUnit, Guid LocationId, string LocationCode, decimal Usable, decimal Point, bool IsDefaulted,
    decimal? PolicyQuantity, Guid? PreferredSupplierId);

internal static class ReorderReads
{
    private static readonly MovementKind[] Consuming = [MovementKind.Sale, MovementKind.Dispense, MovementKind.ClinicalUse, MovementKind.BoardingUse];

    public static async Task<IReadOnlyList<Guid>> ActiveBranchIdsAsync(CatEcommerceDbContext db, Guid? only = null) =>
        await db.Branches.AsNoTracking().Where(b => b.IsActive && (only == null || b.Id == only)).Select(b => b.Id).ToListAsync();

    /// <summary>The pairs of a branch: every position row at an active, non-quarantine location, plus every pair that has a policy. Usable is
    /// <see cref="StockUsability"/> against the branch's local today; the point is the policy's, else <c>inventory.defaultReorderPoint</c>.</summary>
    public static async Task<List<ReorderPair>> PairsAsync(
        CatEcommerceDbContext db, IInventoryClock clock, IBusinessSettings settings, Guid branchId, Guid? locationId)
    {
        var today = await clock.LocalTodayAsync(branchId, CancellationToken.None);
        var defaultPoint = settings.GetDecimal(SettingKeys.InventoryDefaultReorderPoint, branchId);

        var locations = await db.StockLocations.AsNoTracking()
            .Where(l => l.BranchId == branchId && l.IsActive && l.Kind != StockLocationKind.Quarantine && (locationId == null || l.Id == locationId))
            .Select(l => new { l.Id, l.Code, l.Kind }).ToListAsync();
        var locationIds = locations.Select(l => l.Id).ToList();
        if (locationIds.Count == 0) return [];

        var positions = await (
            from p in db.StockPositions.AsNoTracking()
            join b in db.Batches.AsNoTracking() on p.BatchId equals b.Id
            where locationIds.Contains(p.LocationId)
            select new { p.SkuId, p.LocationId, p.OnHand, p.Reserved, b.Status, b.ExpiresOn, b.SkuCode, b.DisplayName, b.StockUnit }).ToListAsync();

        var policies = await db.ReorderPolicies.AsNoTracking().Where(p => locationIds.Contains(p.LocationId))
            .Select(p => new { p.SkuId, p.LocationId, p.ReorderPoint, p.ReorderQuantity, p.PreferredSupplierId }).ToListAsync();

        var kindOf = locations.ToDictionary(l => l.Id, l => l.Kind);
        var codeOf = locations.ToDictionary(l => l.Id, l => l.Code);

        var usable = positions.GroupBy(p => (p.SkuId, p.LocationId)).ToDictionary(
            g => g.Key, g => g.Sum(p => StockUsability.Usable(p.OnHand, p.Reserved, p.Status, kindOf[p.LocationId], p.ExpiresOn, today)));
        var names = positions.GroupBy(p => p.SkuId).ToDictionary(g => g.Key, g => g.First());

        var keys = usable.Keys.Concat(policies.Select(p => (p.SkuId, p.LocationId))).Distinct().ToList();

        var missingNames = keys.Where(k => !names.ContainsKey(k.SkuId)).Select(k => k.SkuId).Distinct().ToList();
        var extraNames = missingNames.Count == 0
            ? new Dictionary<Guid, (string SkuCode, string DisplayName, string StockUnit)>()
            : (await db.Batches.AsNoTracking().Where(b => missingNames.Contains(b.SkuId)).Select(b => new { b.SkuId, b.SkuCode, b.DisplayName, b.StockUnit }).Distinct().ToListAsync())
                .GroupBy(b => b.SkuId).ToDictionary(g => g.Key, g => (g.First().SkuCode, g.First().DisplayName, g.First().StockUnit));

        return keys.Select(k =>
        {
            var policy = policies.FirstOrDefault(p => p.SkuId == k.SkuId && p.LocationId == k.LocationId);
            var (code, display, unit) = names.TryGetValue(k.SkuId, out var n) ? (n.SkuCode, n.DisplayName, n.StockUnit)
                : extraNames.TryGetValue(k.SkuId, out var e) ? e : (string.Empty, string.Empty, string.Empty);

            return new ReorderPair(
                branchId, k.SkuId, code, display, unit, k.LocationId, codeOf[k.LocationId], usable.GetValueOrDefault(k), policy?.ReorderPoint ?? defaultPoint, policy is null,
                policy?.ReorderQuantity, policy?.PreferredSupplierId);
        }).OrderBy(p => p.DisplayName).ThenBy(p => p.LocationCode).ToList();
    }

    /// <summary>Σ |consuming entries| per (SKU, location) of the branch in the last <paramref name="windowDays"/> days.</summary>
    public static async Task<Dictionary<(Guid SkuId, Guid LocationId), decimal>> ConsumptionAsync(CatEcommerceDbContext db, Guid branchId, int windowDays, DateTimeOffset now)
    {
        var since = now.AddDays(-windowDays);
        var rows = await db.StockLedgerEntries.AsNoTracking()
            .Where(e => e.BranchId == branchId && e.OccurredOn >= since && Consuming.Contains(e.Kind))
            .GroupBy(e => new { e.SkuId, e.LocationId })
            .Select(g => new { g.Key.SkuId, g.Key.LocationId, Quantity = g.Sum(e => -e.QuantityDelta) }).ToListAsync();
        return rows.ToDictionary(r => (r.SkuId, r.LocationId), r => r.Quantity);
    }

    /// <summary>What is still due on ISSUED purchase orders of the branch, per (SKU, delivery location): <c>max(0, ordered - accepted)</c> per line (ADR-112).</summary>
    public static async Task<Dictionary<(Guid SkuId, Guid LocationId), decimal>> OnOrderAsync(CatEcommerceDbContext db, Guid branchId)
    {
        var lines = await db.PurchaseOrders.AsNoTracking()
            .Where(p => p.BranchId == branchId && p.Status == PurchaseOrderStatus.Issued)
            .SelectMany(p => p.Lines.Select(l => new { LineId = l.Id, l.SkuId, p.DeliverToLocationId, l.OrderedQuantity })).ToListAsync();
        if (lines.Count == 0) return [];

        var lineIds = lines.Select(l => l.LineId).ToList();
        var accepted = (await db.GoodsReceipts.AsNoTracking().SelectMany(g => g.Lines)
            .Where(l => l.PurchaseOrderLineId != null && lineIds.Contains(l.PurchaseOrderLineId.Value))
            .GroupBy(l => l.PurchaseOrderLineId!.Value).Select(g => new { LineId = g.Key, Accepted = g.Sum(l => l.QuantityAccepted) }).ToListAsync())
            .ToDictionary(a => a.LineId, a => a.Accepted);

        return lines.GroupBy(l => (l.SkuId, l.DeliverToLocationId))
            .ToDictionary(g => g.Key, g => g.Sum(l => PurchaseOrderProgress.ForLine(l.OrderedQuantity, accepted.GetValueOrDefault(l.LineId), 0m).Outstanding));
    }
}

/// <summary>Worker -- §8.13. Per active branch, batches with stock there: the tightest crossed threshold not yet raised, or an expiry not yet raised. Keys already
/// present are filtered out in one query, so an idle pass dispatches nothing.</summary>
public sealed class GetExpiryAlertCandidatesQueryHandler(CatEcommerceDbContext db, IInventoryClock clock, IBusinessSettings settings)
    : IQueryHandlerAsync<GetExpiryAlertCandidatesQuery, IReadOnlyList<ExpiryAlertCandidate>>
{
    public async Task<QueryResponse<IReadOnlyList<ExpiryAlertCandidate>>> HandleAsync(GetExpiryAlertCandidatesQuery query)
    {
        var thresholds = ExpiryAlertRules.Thresholds(
            settings.GetInt(SettingKeys.InventoryExpiryAlertFirstDays), settings.GetInt(SettingKeys.InventoryExpiryAlertSecondDays), settings.GetInt(SettingKeys.InventoryExpiryAlertThirdDays));

        var candidates = new List<ExpiryAlertCandidate>();
        foreach (var branchId in await ReorderReads.ActiveBranchIdsAsync(db, query.BranchId))
        {
            var today = await clock.LocalTodayAsync(branchId, CancellationToken.None);

            var batches = await (
                from p in db.StockPositions.AsNoTracking()
                join l in db.StockLocations.AsNoTracking() on p.LocationId equals l.Id
                join b in db.Batches.AsNoTracking() on p.BatchId equals b.Id
                where l.BranchId == branchId && p.OnHand > 0m && b.ExpiresOn != null
                select new { b.Id, b.SkuId, b.ExpiresOn }).Distinct().ToListAsync();

            foreach (var batch in batches)
            {
                var expiresOn = batch.ExpiresOn!.Value;
                if (BatchExpiryRules.IsExpired(expiresOn, today))
                {
                    candidates.Add(new ExpiryAlertCandidate(branchId, batch.Id, batch.SkuId, null, true, $"expired:{batch.Id}:{branchId}"));
                    continue;
                }

                if (ExpiryAlertRules.TightestCrossed(expiresOn.DayNumber - today.DayNumber, thresholds) is { } threshold)
                    candidates.Add(new ExpiryAlertCandidate(branchId, batch.Id, batch.SkuId, threshold, false, $"expiry:{batch.Id}:{branchId}:{threshold}"));
            }
        }

        var keys = candidates.Select(c => c.DedupKey).ToList();
        var existing = keys.Count == 0
            ? new HashSet<string>()
            : (await db.InventoryAlerts.AsNoTracking().Where(a => a.DedupKey != null && keys.Contains(a.DedupKey)).Select(a => a.DedupKey!).ToListAsync()).ToHashSet();

        return new QueryResponse<IReadOnlyList<ExpiryAlertCandidate>> { Result = candidates.Where(c => !existing.Contains(c.DedupKey)).ToList() };
    }
}

public sealed class GetLowStockCandidatesQueryHandler(CatEcommerceDbContext db, IInventoryClock clock, IBusinessSettings settings)
    : IQueryHandlerAsync<GetLowStockCandidatesQuery, IReadOnlyList<LowStockCandidate>>
{
    public async Task<QueryResponse<IReadOnlyList<LowStockCandidate>>> HandleAsync(GetLowStockCandidatesQuery query)
    {
        var candidates = new List<LowStockCandidate>();
        foreach (var branchId in await ReorderReads.ActiveBranchIdsAsync(db, query.BranchId))
        {
            var live = (await db.InventoryAlerts.AsNoTracking()
                .Where(a => a.BranchId == branchId && a.Kind == StockAlertKind.LowStock && a.Status != InventoryAlertStatus.Resolved)
                .Select(a => new { a.SkuId, a.LocationId }).ToListAsync()).Select(a => (a.SkuId!.Value, a.LocationId!.Value)).ToHashSet();

            candidates.AddRange((await ReorderReads.PairsAsync(db, clock, settings, branchId, null))
                .Where(p => p.Point > 0m && p.Usable < p.Point && !live.Contains((p.SkuId, p.LocationId)))
                .Select(p => new LowStockCandidate(branchId, p.SkuId, p.LocationId, p.Usable, p.Point)));
        }

        return new QueryResponse<IReadOnlyList<LowStockCandidate>> { Result = candidates };
    }
}

public sealed class GetRecoveredLowStockAlertsQueryHandler(CatEcommerceDbContext db, IInventoryClock clock, IBusinessSettings settings)
    : IQueryHandlerAsync<GetRecoveredLowStockAlertsQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetRecoveredLowStockAlertsQuery query)
    {
        var live = await db.InventoryAlerts.AsNoTracking()
            .Where(a => a.Kind == StockAlertKind.LowStock && a.Status != InventoryAlertStatus.Resolved && (query.BranchId == null || a.BranchId == query.BranchId))
            .Select(a => new { a.Id, a.BranchId, a.SkuId, a.LocationId }).ToListAsync();

        var recovered = new List<Guid>();
        foreach (var branch in live.GroupBy(a => a.BranchId))
        {
            var pairs = (await ReorderReads.PairsAsync(db, clock, settings, branch.Key, null)).ToDictionary(p => (p.SkuId, p.LocationId));
            foreach (var alert in branch)
            {
                if (!pairs.TryGetValue((alert.SkuId!.Value, alert.LocationId!.Value), out var pair)) continue;
                if (pair.Point <= 0m || pair.Usable >= pair.Point) recovered.Add(alert.Id);
            }
        }

        return new QueryResponse<IReadOnlyList<Guid>> { Result = recovered };
    }
}

public sealed class GetStockAlertsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStockAlertsQuery, PagedResult<InventoryAlertViewModel>>
{
    public async Task<QueryResponse<PagedResult<InventoryAlertViewModel>>> HandleAsync(GetStockAlertsQuery query)
    {
        var alerts = db.InventoryAlerts.AsNoTracking().Where(a => a.BranchId == query.BranchId);

        if (EnumerationParsing.TryParse<InventoryAlertStatus>(query.Status, out var status) && status is not null) alerts = alerts.Where(a => a.Status == status);
        if (EnumerationParsing.TryParse<StockAlertKind>(query.Kind, out var kind) && kind is not null) alerts = alerts.Where(a => a.Kind == kind);
        if (query.LocationId is { } locationId) alerts = alerts.Where(a => a.LocationId == locationId);

        var total = await alerts.CountAsync();
        var page = await alerts.OrderByDescending(a => a.RaisedOn).ThenBy(a => a.Id).Skip(query.Skip).Take(query.Take)
            .Select(a => new { a.Id, a.Kind, a.Status, a.SkuId, a.BatchId, a.LocationId, a.ThresholdDays, a.QuantityAtRaise, a.RaisedOn, a.AcknowledgedBy, a.AcknowledgedOn, a.ResolvedOn, a.ResolutionNote }).ToListAsync();

        var batchIds = page.Where(a => a.BatchId != null).Select(a => a.BatchId!.Value).Distinct().ToList();
        var batches = await db.Batches.AsNoTracking().Where(b => batchIds.Contains(b.Id)).Select(b => new { b.Id, b.SkuId, b.SkuCode, b.DisplayName, b.LotNumber }).ToListAsync();
        var skuIds = page.Where(a => a.SkuId != null).Select(a => a.SkuId!.Value).Distinct().ToList();
        var skuNames = (await db.Batches.AsNoTracking().Where(b => skuIds.Contains(b.SkuId)).Select(b => new { b.SkuId, b.SkuCode, b.DisplayName }).Distinct().ToListAsync())
            .GroupBy(b => b.SkuId).ToDictionary(g => g.Key, g => g.First());
        var locationIds = page.Where(a => a.LocationId != null).Select(a => a.LocationId!.Value).Distinct().ToList();
        var codes = await db.StockLocations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).Select(l => new { Key = l.Id, Value = l.Code }).ToDictionaryAsync(x => x.Key, x => x.Value);

        var items = page.Select(a =>
        {
            var batch = a.BatchId is { } b ? batches.FirstOrDefault(x => x.Id == b) : null;
            var sku = a.SkuId is { } s && skuNames.TryGetValue(s, out var found) ? found : null;
            return new InventoryAlertViewModel(
                a.Id, a.Kind.DisplayName, a.Status.DisplayName, a.SkuId ?? batch?.SkuId, sku?.SkuCode ?? batch?.SkuCode, sku?.DisplayName ?? batch?.DisplayName, a.BatchId,
                batch?.LotNumber, a.LocationId, a.LocationId is { } l ? codes.GetValueOrDefault(l) : null, a.ThresholdDays, InventoryViewFormat.Quantity(a.QuantityAtRaise), a.RaisedOn,
                a.AcknowledgedBy, a.AcknowledgedOn, a.ResolvedOn, a.ResolutionNote);
        }).ToList();

        return new QueryResponse<PagedResult<InventoryAlertViewModel>> { Result = new PagedResult<InventoryAlertViewModel>(items, query.PageNumber, query.PageSize, total) };
    }
}

public sealed class GetStockAlertSummaryQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStockAlertSummaryQuery, StockAlertSummaryViewModel>
{
    public async Task<QueryResponse<StockAlertSummaryViewModel>> HandleAsync(GetStockAlertSummaryQuery query)
    {
        var rows = await db.InventoryAlerts.AsNoTracking()
            .Where(a => a.BranchId == query.BranchId && a.Status == InventoryAlertStatus.Open)
            .GroupBy(a => a.Kind).Select(g => new { Kind = g.Key, Count = g.Count() }).ToListAsync();

        var byKind = rows.ToDictionary(r => r.Kind.DisplayName, r => r.Count);
        return new QueryResponse<StockAlertSummaryViewModel> { Result = new StockAlertSummaryViewModel(new OpenAlertSummaryViewModel(byKind.Values.Sum(), byKind)) };
    }
}

public sealed class GetReorderPoliciesQueryHandler(CatEcommerceDbContext db, IInventoryClock clock, IBusinessSettings settings)
    : IQueryHandlerAsync<GetReorderPoliciesQuery, PagedResult<ReorderPolicyViewModel>>
{
    public async Task<QueryResponse<PagedResult<ReorderPolicyViewModel>>> HandleAsync(GetReorderPoliciesQuery query)
    {
        var pairs = await ReorderReads.PairsAsync(db, clock, settings, query.BranchId, query.LocationId);

        var supplierIds = pairs.Where(p => p.PreferredSupplierId != null).Select(p => p.PreferredSupplierId!.Value).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).Select(s => new { Key = s.Id, Value = s.Name }).ToDictionaryAsync(x => x.Key, x => x.Value);

        var page = pairs.Skip(query.Skip).Take(query.Take).Select(p => new ReorderPolicyViewModel(
            p.SkuId, p.SkuCode, p.DisplayName, p.LocationId, p.LocationCode, InventoryViewFormat.Quantity(p.Point), p.IsDefaulted, InventoryViewFormat.Quantity(p.PolicyQuantity),
            p.PreferredSupplierId, p.PreferredSupplierId is { } s ? suppliers.GetValueOrDefault(s) : null)).ToList();

        return new QueryResponse<PagedResult<ReorderPolicyViewModel>> { Result = new PagedResult<ReorderPolicyViewModel>(page, query.PageNumber, query.PageSize, pairs.Count) };
    }
}

/// <summary>US-IMP-12 AC2 -- the suggestion per (SKU, location) pair that needs stock: the pure <see cref="ReorderSuggestionCalculator"/> over the window's
/// consumption and what is still due on issued purchase orders.</summary>
public sealed class GetReorderSuggestionsQueryHandler(
    CatEcommerceDbContext db, IInventoryClock clock, IBusinessSettings settings, ReorderSuggestionCalculator calculator, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetReorderSuggestionsQuery, IReadOnlyList<ReorderSuggestionViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<ReorderSuggestionViewModel>>> HandleAsync(GetReorderSuggestionsQuery query)
    {
        var windowDays = settings.GetInt(SettingKeys.InventoryConsumptionWindowDays);
        var coverDays = settings.GetInt(SettingKeys.InventoryReorderCoverDays, query.BranchId);
        var defaultLead = settings.GetInt(SettingKeys.InventoryDefaultLeadTimeDays);

        var pairs = await ReorderReads.PairsAsync(db, clock, settings, query.BranchId, query.LocationId);
        var consumption = await ReorderReads.ConsumptionAsync(db, query.BranchId, windowDays, timeProvider.GetUtcNow());
        var onOrder = await ReorderReads.OnOrderAsync(db, query.BranchId);

        var supplierIds = pairs.Where(p => p.PreferredSupplierId != null).Select(p => p.PreferredSupplierId!.Value).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).Select(s => new { s.Id, s.Name, s.LeadTimeDays }).ToDictionaryAsync(s => s.Id, s => new { s.Name, s.LeadTimeDays });

        var rows = new List<ReorderSuggestionViewModel>();
        foreach (var pair in pairs)
        {
            if (query.SupplierId is { } wanted && pair.PreferredSupplierId != wanted) continue;

            var supplier = pair.PreferredSupplierId is { } sid ? suppliers.GetValueOrDefault(sid) : null;
            var lead = supplier?.LeadTimeDays ?? defaultLead;
            var key = (pair.SkuId, pair.LocationId);

            var suggestion = calculator.Suggest(new ReorderInput(
                consumption.GetValueOrDefault(key), windowDays, lead, coverDays, pair.Point, pair.Usable, onOrder.GetValueOrDefault(key), pair.PolicyQuantity));

            if (suggestion.SuggestedQuantity <= 0m && pair.Usable >= pair.Point) continue;
            if (pair.Point <= 0m && pair.PolicyQuantity is null && suggestion.SuggestedQuantity <= 0m) continue;

            rows.Add(new ReorderSuggestionViewModel(
                pair.SkuId, pair.SkuCode, pair.DisplayName, pair.StockUnit, pair.LocationId, pair.LocationCode, InventoryViewFormat.Quantity(pair.Usable),
                InventoryViewFormat.Quantity(onOrder.GetValueOrDefault(key)), InventoryViewFormat.Quantity(pair.Point), pair.IsDefaulted,
                suggestion.AverageDailyConsumption.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture), lead, coverDays,
                InventoryViewFormat.Quantity(suggestion.SuggestedQuantity), pair.PreferredSupplierId, supplier?.Name));
        }

        return new QueryResponse<IReadOnlyList<ReorderSuggestionViewModel>> { Result = rows };
    }
}

/// <summary>US-IMP-10 AC2 -- per batch with stock in the branch that expires within the window: days remaining (negative once expired), the quantity per location,
/// and the value at risk = on hand x the unit cost of <c>v_batch_unit_cost</c> (landed when it exists).</summary>
public sealed class GetNearExpiryReportQueryHandler(CatEcommerceDbContext db, IInventoryClock clock, IBusinessSettings settings)
    : IQueryHandlerAsync<GetNearExpiryReportQuery, NearExpiryReportViewModel>
{
    public async Task<QueryResponse<NearExpiryReportViewModel>> HandleAsync(GetNearExpiryReportQuery query)
    {
        var today = await clock.LocalTodayAsync(query.BranchId, CancellationToken.None);
        var currency = settings.GetString(SettingKeys.CommerceCurrencyCode, query.BranchId);
        var thresholds = ExpiryAlertRules.Thresholds(
            settings.GetInt(SettingKeys.InventoryExpiryAlertFirstDays), settings.GetInt(SettingKeys.InventoryExpiryAlertSecondDays), settings.GetInt(SettingKeys.InventoryExpiryAlertThirdDays));
        var withinDays = query.WithinDays ?? (thresholds.Count > 0 ? thresholds[0] : 90);

        var rows = await (
            from p in db.StockPositions.AsNoTracking()
            join l in db.StockLocations.AsNoTracking() on p.LocationId equals l.Id
            join b in db.Batches.AsNoTracking() on p.BatchId equals b.Id
            where l.BranchId == query.BranchId && p.OnHand > 0m && b.ExpiresOn != null
            select new { b.Id, b.SkuId, b.SkuCode, b.DisplayName, b.LotNumber, ExpiresOn = b.ExpiresOn!.Value, b.Status, LocationId = l.Id, LocationCode = l.Code, p.OnHand }).ToListAsync();

        var batchIds = rows.Select(r => r.Id).Distinct().ToList();
        var costs = await db.BatchUnitCosts.AsNoTracking().Where(c => batchIds.Contains(c.BatchId)).ToDictionaryAsync(c => c.BatchId);

        var report = rows.GroupBy(r => r.Id).Select(g => new { First = g.First(), Quantity = g.Sum(r => r.OnHand), Locations = g.ToList() })
            .Select(g => new { g.First, g.Quantity, g.Locations, Days = g.First.ExpiresOn.DayNumber - today.DayNumber })
            .Where(g => g.Days <= withinDays && (query.IncludeExpired || g.Days >= 0))
            .OrderBy(g => g.First.ExpiresOn).ThenBy(g => g.First.SkuCode).ThenBy(g => g.First.LotNumber)
            .Select(g =>
            {
                var cost = costs.GetValueOrDefault(g.First.Id);
                var unit = cost?.UnitCostBase ?? 0m;
                return new NearExpiryRowViewModel(
                    g.First.SkuId, g.First.SkuCode, g.First.DisplayName, g.First.Id, g.First.LotNumber, g.First.ExpiresOn, g.Days, BatchExpiryRules.IsExpired(g.First.ExpiresOn, today),
                    g.First.Status.DisplayName, InventoryViewFormat.Quantity(g.Quantity),
                    g.Locations.Select(l => new LocationQuantityViewModel(l.LocationId, l.LocationCode, InventoryViewFormat.Quantity(l.OnHand))).ToList(),
                    InventoryViewFormat.UnitCost(unit, currency), cost?.CostBasis ?? "Purchase", InventoryViewFormat.Money(decimal.Round(g.Quantity * unit, 2, MidpointRounding.ToEven), currency));
            }).ToList();

        var total = report.Sum(r => decimal.Parse(r.ValueAtRisk.Amount, System.Globalization.CultureInfo.InvariantCulture));
        return new QueryResponse<NearExpiryReportViewModel>
        { Result = new NearExpiryReportViewModel(today, withinDays, query.IncludeExpired, InventoryViewFormat.Money(total, currency), report) };
    }
}

/// <summary>SRS §8.1 -- per SKU x batch x location with the unit cost, its basis (<c>Landed</c> or <c>Purchase</c>) and the value.</summary>
public sealed class GetStockOnHandReportQueryHandler(CatEcommerceDbContext db, IInventoryClock clock, IBusinessSettings settings)
    : IQueryHandlerAsync<GetStockOnHandReportQuery, StockOnHandReportViewModel>
{
    public async Task<QueryResponse<StockOnHandReportViewModel>> HandleAsync(GetStockOnHandReportQuery query)
    {
        var today = await clock.LocalTodayAsync(query.BranchId, CancellationToken.None);
        var currency = settings.GetString(SettingKeys.CommerceCurrencyCode, query.BranchId);

        var positions = from p in db.StockPositions.AsNoTracking()
                        join l in db.StockLocations.AsNoTracking() on p.LocationId equals l.Id
                        join b in db.Batches.AsNoTracking() on p.BatchId equals b.Id
                        where l.BranchId == query.BranchId && p.OnHand > 0m
                        select new { b.Id, b.SkuId, b.SkuCode, b.DisplayName, b.StockClass, b.LotNumber, b.ExpiresOn, b.Status, LocationId = l.Id, LocationCode = l.Code, p.OnHand, p.Reserved };

        if (query.LocationId is { } locationId) positions = positions.Where(p => p.LocationId == locationId);
        if (EnumerationParsing.TryParse<StockClass>(query.Class, out var stockClass) && stockClass is not null) positions = positions.Where(p => p.StockClass == stockClass);

        var rows = await positions.OrderBy(p => p.DisplayName).ThenBy(p => p.LotNumber).ThenBy(p => p.LocationCode).ToListAsync();
        var batchIds = rows.Select(r => r.Id).Distinct().ToList();
        var costs = await db.BatchUnitCosts.AsNoTracking().Where(c => batchIds.Contains(c.BatchId)).ToDictionaryAsync(c => c.BatchId);

        var items = rows.Select(r =>
        {
            var cost = costs.GetValueOrDefault(r.Id);
            var unit = cost?.UnitCostBase ?? 0m;
            return new StockOnHandRowViewModel(
                r.SkuId, r.SkuCode, r.DisplayName, r.StockClass.DisplayName, r.Id, r.LotNumber, r.ExpiresOn, r.Status.DisplayName, r.LocationId, r.LocationCode,
                InventoryViewFormat.Quantity(r.OnHand), InventoryViewFormat.Quantity(r.Reserved), InventoryViewFormat.UnitCost(unit, currency), cost?.CostBasis ?? "Purchase",
                InventoryViewFormat.Money(decimal.Round(r.OnHand * unit, 2, MidpointRounding.ToEven), currency));
        }).ToList();

        var total = items.Sum(i => decimal.Parse(i.Value.Amount, System.Globalization.CultureInfo.InvariantCulture));
        return new QueryResponse<StockOnHandReportViewModel> { Result = new StockOnHandReportViewModel(today, InventoryViewFormat.Money(total, currency), items) };
    }
}

internal sealed class ReconciliationRow
{
    public Guid BatchId { get; set; }
    public Guid LocationId { get; set; }
    public decimal OnHand { get; set; }
    public decimal ExpectedOnHand { get; set; }
    public decimal Reserved { get; set; }
    public decimal ExpectedReserved { get; set; }
    public Guid SkuId { get; set; }
    public Guid BranchId { get; set; }
}

/// <summary>§8.13 -- the position projection against the entries it is derived from. The expected result is EMPTY, forever; a row is a ledger bug.</summary>
public sealed class GetStockReconciliationQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetStockReconciliationQuery, IReadOnlyList<StockReconciliationRowViewModel>>
{
    private const string Sql = """
        SELECT p.batch_id AS "BatchId", p.location_id AS "LocationId", p.on_hand AS "OnHand", COALESCE(l.total, 0) AS "ExpectedOnHand",
               p.reserved AS "Reserved", COALESCE(r.total, 0) + COALESCE(l.consumed, 0) AS "ExpectedReserved", p.sku_id AS "SkuId", sl.branch_id AS "BranchId"
          FROM inventory.stock_position p
          JOIN inventory.stock_location sl ON sl."Id" = p.location_id
          LEFT JOIN (SELECT batch_id, location_id, SUM(quantity_delta) AS total, SUM(quantity_delta) FILTER (WHERE consumes_reservation) AS consumed
                       FROM inventory.stock_ledger_entry GROUP BY batch_id, location_id) l ON l.batch_id = p.batch_id AND l.location_id = p.location_id
          LEFT JOIN (SELECT batch_id, location_id, SUM(quantity_delta) AS total
                       FROM inventory.stock_reservation_entry GROUP BY batch_id, location_id) r ON r.batch_id = p.batch_id AND r.location_id = p.location_id
         WHERE (@branch IS NULL OR sl.branch_id = @branch)
           AND (p.on_hand <> COALESCE(l.total, 0) OR p.reserved <> COALESCE(r.total, 0) + COALESCE(l.consumed, 0))
        """;

    public async Task<QueryResponse<IReadOnlyList<StockReconciliationRowViewModel>>> HandleAsync(GetStockReconciliationQuery query)
    {
        var branch = new Npgsql.NpgsqlParameter("branch", NpgsqlTypes.NpgsqlDbType.Uuid) { Value = (object?)query.BranchId ?? DBNull.Value };
        var rows = await db.Database.SqlQueryRaw<ReconciliationRow>(Sql, branch).ToListAsync();

        IReadOnlyList<StockReconciliationRowViewModel> result = rows.Select(r => new StockReconciliationRowViewModel(
            r.BatchId, r.LocationId, InventoryViewFormat.Quantity(r.OnHand), InventoryViewFormat.Quantity(r.ExpectedOnHand), InventoryViewFormat.Quantity(r.Reserved),
            InventoryViewFormat.Quantity(r.ExpectedReserved), r.SkuId, r.BranchId)).ToList();

        return new QueryResponse<IReadOnlyList<StockReconciliationRowViewModel>> { Result = result };
    }
}
