using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Application.Tests.TestSupport;

/// <summary>Hand-written in-memory doubles of Inventory's ports (no mocking library is in scope). Each is a plain data holder a test fills in
/// and a handler reads.</summary>
public sealed class FakeSkuDirectory : ISkuDirectory
{
    public Dictionary<Guid, SkuStockProfile> Skus { get; } = [];

    public SkuStockProfile Add(
        bool expiryRequired = false, StockClass? stockClass = null, bool fractional = false, bool active = true, string? barcode = null)
    {
        var sku = new SkuStockProfile(
            Guid.NewGuid(), Guid.NewGuid(), "SKU-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), "Test product", barcode, stockClass ?? StockClass.General,
            expiryRequired, "each", fractional, null, active);
        Skus[sku.SkuId] = sku;
        return sku;
    }

    public Task<SkuStockProfile?> GetAsync(Guid skuId, CancellationToken ct) => Task.FromResult(Skus.GetValueOrDefault(skuId));

    public Task<IReadOnlyList<SkuStockProfile>> GetManyAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SkuStockProfile>>(skuIds.Where(Skus.ContainsKey).Select(id => Skus[id]).ToList());

    public Task<SkuStockProfile?> FindByBarcodeAsync(string barcode, CancellationToken ct) =>
        Task.FromResult(Skus.Values.FirstOrDefault(s => s.Barcode == barcode));

    public Task<IReadOnlyList<SkuStockProfile>> SearchAsync(string text, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SkuStockProfile>>(Skus.Values.Where(s => s.SkuCode.Contains(text, StringComparison.OrdinalIgnoreCase)).Take(take).ToList());

    public Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct) => Task.FromResult(Skus.Values.Any(s => s.ProductId == productId));
}

public sealed class FakeInventoryClock(DateOnly today) : IInventoryClock
{
    public DateOnly Today { get; set; } = today;

    public Task<DateOnly> LocalTodayAsync(Guid branchId, CancellationToken ct) => Task.FromResult(Today);

    public Task<string> TimeZoneIdAsync(Guid branchId, CancellationToken ct) => Task.FromResult("Asia/Dhaka");

    public Task<DateOnly> LocalDateOfAsync(Guid branchId, DateTimeOffset instant, CancellationToken ct) => Task.FromResult(Today);
}

public sealed class FakeDocumentNumbers : IDocumentNumberGenerator
{
    private int _next;

    public Task<string> NextAsync(DocumentKind kind, CancellationToken ct) => Task.FromResult($"{kind.DisplayName[..3].ToUpperInvariant()}-2026-{++_next:D6}");
}

public sealed class FakeStockPositionReader : IStockPositionReader
{
    public Dictionary<Guid, StockLocationFact> Locations { get; } = [];
    public List<StockCandidate> Candidates { get; } = [];
    public List<StockPositionRow> Positions { get; } = [];
    public Dictionary<Guid, BatchFact> Batches { get; } = [];
    public Dictionary<Guid, decimal> UnitCosts { get; } = [];
    public HashSet<Guid> PurchaseOrdersWithReceipts { get; } = [];
    public List<PurchaseOrderLineReceipt> PurchaseOrderReceipts { get; } = [];
    public List<ConsignmentBatchFact> ConsignmentBatches { get; } = [];

    public StockLocationFact AddLocation(Guid branchId, StockLocationKind kind, bool active = true)
    {
        var location = new StockLocationFact(Guid.NewGuid(), branchId, "L" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), kind, active);
        Locations[location.LocationId] = location;
        return location;
    }

    public Task<IReadOnlyList<StockCandidate>> GetCandidatesAsync(Guid branchId, Guid skuId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockCandidate>>(Candidates.ToList());

    public Task<IReadOnlyList<StockPositionRow>> GetPositionsAtLocationAsync(Guid locationId, IReadOnlyCollection<Guid>? skuIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockPositionRow>>(Positions.Where(p => p.LocationId == locationId && p.OnHand > 0m && (skuIds is null || skuIds.Contains(p.SkuId))).ToList());

    public Task<bool> LocationHasStockAsync(Guid locationId, CancellationToken ct) =>
        Task.FromResult(Positions.Any(p => p.LocationId == locationId && p.OnHand > 0m));

    public Task<IReadOnlyDictionary<Guid, BatchFact>> GetBatchFactsAsync(IReadOnlyCollection<Guid> batchIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, BatchFact>>(Batches.Where(b => batchIds.Contains(b.Key)).ToDictionary(b => b.Key, b => b.Value));

    public Task<IReadOnlyDictionary<Guid, BatchUnitCost>> GetUnitCostsAsync(IReadOnlyCollection<Guid> batchIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, BatchUnitCost>>(UnitCosts.Where(c => batchIds.Contains(c.Key)).ToDictionary(c => c.Key, c => new BatchUnitCost(c.Value, "Purchase")));

    public List<StockPositionRow> BatchPositions { get; } = [];

    public Task<IReadOnlyList<StockPositionRow>> GetPositionsOfBatchAsync(Guid batchId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockPositionRow>>(BatchPositions.Where(p => p.BatchId == batchId).ToList());

    public Task<IReadOnlyDictionary<Guid, StockLocationFact>> GetLocationsAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, StockLocationFact>>(Locations.Where(l => locationIds.Contains(l.Key)).ToDictionary(l => l.Key, l => l.Value));

    public Task<StockLocationFact?> GetQuarantineLocationAsync(Guid branchId, CancellationToken ct) =>
        Task.FromResult(Locations.Values.FirstOrDefault(l => l.BranchId == branchId && l.Kind == StockLocationKind.Quarantine && l.IsActive));

    public Task<IReadOnlyList<ReservationRemaining>> GetRemainingReservationsAsync(StockReferenceKind referenceKind, Guid referenceId, Guid? referenceLineId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReservationRemaining>>([]);

    public Task<bool> WasConsumedByPartyAsync(Guid batchId, Guid? customerId, Guid? catId, CancellationToken ct) => Task.FromResult(false);

    public Task<IReadOnlyList<PurchaseOrderLineReceipt>> GetPurchaseOrderReceiptsAsync(Guid purchaseOrderId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PurchaseOrderLineReceipt>>(PurchaseOrderReceipts.ToList());

    public Task<bool> PurchaseOrderHasReceiptsAsync(Guid purchaseOrderId, CancellationToken ct) => Task.FromResult(PurchaseOrdersWithReceipts.Contains(purchaseOrderId));

    public Task<IReadOnlyDictionary<Guid, PurchaseOrderReceiptState>> GetPurchaseOrderReceiptStatesAsync(IReadOnlyCollection<Guid> purchaseOrderIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, PurchaseOrderReceiptState>>(purchaseOrderIds.ToDictionary(id => id, _ => PurchaseOrderReceiptState.NotReceived));

    public Task<IReadOnlyList<PurchaseOrderReceiptFact>> GetPurchaseOrderReceiptHeadersAsync(Guid purchaseOrderId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PurchaseOrderReceiptFact>>([]);

    public Task<IReadOnlyList<ConsignmentBatchFact>> GetConsignmentBatchesAsync(Guid consignmentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ConsignmentBatchFact>>(ConsignmentBatches.ToList());
}

public sealed class FakeBatchRegistry : IBatchRegistry
{
    public List<BatchAggregate> Enlisted { get; } = [];

    public void Enlist(BatchAggregate batch) => Enlisted.Add(batch);
}

/// <summary>Records every draft it is given; <see cref="Refusal"/> makes the next append fail like the real writer's validation would.</summary>
public sealed class FakeLedgerWriter : IInventoryLedgerWriter
{
    public List<StockMovementDraft> Appended { get; } = [];
    public StockResult<Unit>? Refusal { get; set; }

    public Task<StockResult<Unit>> AppendAsync(IReadOnlyList<StockMovementDraft> drafts, CancellationToken ct)
    {
        if (Refusal is not null) return Task.FromResult(Refusal);
        Appended.AddRange(drafts);
        return Task.FromResult(StockResult<Unit>.Success(Unit.Value));
    }
}
