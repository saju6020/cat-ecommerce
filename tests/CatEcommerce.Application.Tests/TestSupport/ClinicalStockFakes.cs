using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Application.Tests.TestSupport;

/// <summary>One batch on the fake dispensary shelf.</summary>
internal sealed class ShelfBatch(Guid skuId, string lot, DateOnly? expiresOn, decimal onHand, decimal reserved = 0m, BatchStatus? status = null)
{
    public Guid SkuId { get; } = skuId;
    public Guid BatchId { get; } = Guid.NewGuid();
    public string Lot { get; } = lot;
    public DateOnly? ExpiresOn { get; } = expiresOn;
    public decimal OnHand { get; set; } = onHand;
    public decimal Reserved { get; } = reserved;
    public BatchStatus Status { get; } = status ?? BatchStatus.Available;
    public decimal Free => OnHand - Reserved;
}

/// <summary>
/// A hand-written <see cref="IStockLedger"/> with a shelf in memory, behaving as HO-IMP-001's <c>EfStockLedger</c> does for the members Phase F
/// uses: <c>PlanAsync</c> is FEFO over usable batches, <c>GetBatchUsabilityAsync</c> refuses an expired or non-available batch, <c>DeductAsync</c>
/// refuses more than a batch holds, and every call a handler makes is recorded so a test can assert it (and the order of the calls). The members
/// Phase F never calls throw, so a handler that starts calling one fails loudly.
/// </summary>
internal sealed class ScriptedStockLedger(DateOnly today) : IStockLedger
{
    public Guid DispensaryLocationId { get; } = Guid.NewGuid();
    public bool HasDispensary { get; set; } = true;
    public List<ShelfBatch> Shelf { get; } = [];

    public List<(StockDemand Demand, StockReference Reference, StockParty Party)> Deducted { get; } = [];
    public List<(StockReturn Return, StockReference Reference, StockParty Party)> Returned { get; } = [];
    public List<StockShortfall> Shortfalls { get; } = [];
    public List<string> Calls { get; } = [];

    /// <summary>When set, <c>DeductAsync</c> answers this failure after validating nothing -- the race the last units lost.</summary>
    public StockResult<IReadOnlyList<StockMovementLine>>? DeductFailure { get; set; }

    public ShelfBatch Stock(Guid skuId, string lot, DateOnly? expiresOn, decimal onHand, decimal reserved = 0m, BatchStatus? status = null)
    {
        var batch = new ShelfBatch(skuId, lot, expiresOn, onHand, reserved, status);
        Shelf.Add(batch);
        return batch;
    }

    private bool Usable(ShelfBatch b) => b.Status == BatchStatus.Available && !BatchExpiryRules.IsExpired(b.ExpiresOn, today) && b.Free > 0m;

    public Task<StockResult<DispensaryLocation>> GetClinicDispensaryAsync(Guid branchId, CancellationToken ct)
    {
        Calls.Add(nameof(GetClinicDispensaryAsync));
        return Task.FromResult(HasDispensary
            ? StockResult<DispensaryLocation>.Success(new DispensaryLocation(DispensaryLocationId, "DISP"))
            : StockResult<DispensaryLocation>.Failure("DispensaryNotFound", "This branch has no active clinic dispensary.", "BranchId"));
    }

    public Task<StockResult<IReadOnlyList<DispensaryBatch>>> GetDispensaryBatchesAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
    {
        Calls.Add(nameof(GetDispensaryBatchesAsync));
        IReadOnlyList<DispensaryBatch> batches = Shelf.Where(b => skuIds.Contains(b.SkuId) && (b.OnHand > 0m || b.Reserved > 0m))
            .Select(b => new DispensaryBatch(b.SkuId, b.BatchId, b.Lot, b.ExpiresOn, b.Status, DispensaryLocationId, b.OnHand, b.Reserved)).ToList();
        return Task.FromResult(StockResult<IReadOnlyList<DispensaryBatch>>.Success(batches));
    }

    public Task<StockResult<IReadOnlyList<BatchUsability>>> GetBatchUsabilityAsync(Guid branchId, IReadOnlyCollection<Guid> batchIds, CancellationToken ct)
    {
        Calls.Add(nameof(GetBatchUsabilityAsync));
        IReadOnlyList<BatchUsability> result = batchIds.Select(id =>
        {
            var batch = Shelf.FirstOrDefault(b => b.BatchId == id);
            if (batch is null) return new BatchUsability(id, Guid.Empty, string.Empty, BusinessViolationCodes.BatchNotFound, "That batch does not exist at this branch.");
            if (batch.Status != BatchStatus.Available) return new BatchUsability(id, batch.SkuId, batch.Lot, BusinessViolationCodes.BatchNotUsable, InventoryMessages.BatchNotUsable);
            if (BatchExpiryRules.IsExpired(batch.ExpiresOn, today))
                return new BatchUsability(id, batch.SkuId, batch.Lot, BusinessViolationCodes.BatchExpired, InventoryMessages.BatchExpired(batch.Lot, batch.ExpiresOn!.Value));
            return new BatchUsability(id, batch.SkuId, batch.Lot, null, null);
        }).ToList();
        return Task.FromResult(StockResult<IReadOnlyList<BatchUsability>>.Success(result));
    }

    public Task<StockResult<StockPlan>> PlanAsync(StockDemand demand, CancellationToken ct)
    {
        Calls.Add(nameof(PlanAsync));
        var remaining = demand.Quantity;
        var lines = new List<StockMovementLine>();
        foreach (var b in Shelf.Where(b => b.SkuId == demand.SkuId && Usable(b)).OrderBy(b => b.ExpiresOn is null).ThenBy(b => b.ExpiresOn).ThenBy(b => b.Lot, StringComparer.Ordinal))
        {
            if (remaining <= 0m) break;
            var take = Math.Min(remaining, b.Free);
            lines.Add(new StockMovementLine(b.BatchId, b.Lot, b.ExpiresOn, DispensaryLocationId, take, FefoOverridden: false));
            remaining -= take;
        }

        return Task.FromResult(StockResult<StockPlan>.Success(new StockPlan(lines, Math.Max(0m, remaining))));
    }

    public Task<StockResult<IReadOnlyList<StockMovementLine>>> DeductAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct)
    {
        Calls.Add(nameof(DeductAsync));
        if (DeductFailure is not null) return Task.FromResult(DeductFailure);

        var lines = new List<StockMovementLine>();
        foreach (var (batchId, _, quantity) in demand.ExplicitLines ?? [])
        {
            var batch = Shelf.FirstOrDefault(b => b.BatchId == batchId);
            if (batch is null || quantity > batch.Free)
                return Task.FromResult(StockResult<IReadOnlyList<StockMovementLine>>.Failure(BusinessViolationCodes.InsufficientStock, InventoryMessages.InsufficientStockAvailable(batch?.Free ?? 0m), "ExplicitLines"));
            lines.Add(new StockMovementLine(batch.BatchId, batch.Lot, batch.ExpiresOn, DispensaryLocationId, quantity, FefoOverridden: demand.OverrideReason is not null));
        }

        foreach (var line in lines) Shelf.First(b => b.BatchId == line.BatchId).OnHand -= line.Quantity;
        Deducted.Add((demand, reference, party));
        return Task.FromResult(StockResult<IReadOnlyList<StockMovementLine>>.Success(lines));
    }

    public Task<StockResult<IReadOnlyList<StockMovementLine>>> ReceiveReturnAsync(StockReturn stockReturn, StockReference reference, StockParty party, CancellationToken ct)
    {
        Calls.Add(nameof(ReceiveReturnAsync));
        Returned.Add((stockReturn, reference, party));
        IReadOnlyList<StockMovementLine> lines = [new StockMovementLine(stockReturn.BatchId, "LOT", null, Guid.NewGuid(), stockReturn.Quantity, false)];
        return Task.FromResult(StockResult<IReadOnlyList<StockMovementLine>>.Success(lines));
    }

    public void RecordShortfall(StockShortfall shortfall)
    {
        Calls.Add(nameof(RecordShortfall));
        Shortfalls.Add(shortfall);
    }

    public Task<StockResult<StockPlan>> ReserveAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct) => throw new NotSupportedException();
    public Task<StockResult<Unit>> ReleaseAsync(StockReference reference, CancellationToken ct) => throw new NotSupportedException();
    public Task<StockResult<IReadOnlyList<StockMovementLine>>> ConsumeReservationAsync(StockReference reference, StockParty party, CancellationToken ct) => throw new NotSupportedException();
    public Task<StockResult<StockPlan>> ReserveBatchAsync(StockBatchDemand demand, StockReference reference, StockParty party, CancellationToken ct) => throw new NotSupportedException();
    public Task<StockResult<IReadOnlyList<StockMovementLine>>> AdjustAsync(StockAdjustment adjustment, StockReference reference, StockParty party, CancellationToken ct) => throw new NotSupportedException();
    public Task<StockResult<IReadOnlyList<BatchBalance>>> GetBranchBalancesAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct) => throw new NotSupportedException();
}

internal sealed class FakeClinicalSkuReader : IClinicalSkuReader
{
    private readonly Dictionary<Guid, ClinicalSku> _skus = [];

    public ClinicalSku Add(string name = "Meloxicam", bool controlled = false, bool active = true, bool fractional = false, string unit = "each", Guid? taxClassId = null)
    {
        var sku = new ClinicalSku(Guid.NewGuid(), name, unit, active, fractional, taxClassId ?? Guid.NewGuid(), controlled);
        _skus[sku.SkuId] = sku;
        return sku;
    }

    public Task<ClinicalSku?> GetAsync(Guid skuId, CancellationToken ct) => Task.FromResult(_skus.GetValueOrDefault(skuId));
}

internal sealed class FakeSkuPriceReader : ISkuPriceReader
{
    public Dictionary<Guid, Money> Prices { get; } = [];
    public List<(Guid SkuId, DateTimeOffset At)> Asked { get; } = [];

    public Task<Money?> PriceAtAsync(Guid skuId, DateTimeOffset at, CancellationToken ct)
    {
        Asked.Add((skuId, at));
        return Task.FromResult(Prices.TryGetValue(skuId, out var price) ? price : null);
    }
}

internal sealed class FakeServicePricing : IBillingPricingInputs
{
    public Dictionary<Guid, ServicePriceInfo> Services { get; } = [];
    public List<(Guid ServiceId, DateTimeOffset At)> Asked { get; } = [];

    public Task<ServicePriceInfo?> GetServicePriceAsync(Guid serviceId, DateTimeOffset at, CancellationToken ct = default)
    {
        Asked.Add((serviceId, at));
        return Task.FromResult(Services.GetValueOrDefault(serviceId));
    }

    public Task<IReadOnlyDictionary<Guid, TaxRateInfo>> GetTaxRatesAsync(IReadOnlyCollection<Guid> taxClassIds, DateTimeOffset at, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Guid?> GetServiceTaxClassIdAsync(Guid serviceId, CancellationToken ct = default) =>
        Task.FromResult<Guid?>(Services.TryGetValue(serviceId, out var s) ? s.TaxClassId : null);
    public Task<IReadOnlyDictionary<Guid, Guid>> GetSkuTaxClassIdsAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default) => throw new NotSupportedException();
}
