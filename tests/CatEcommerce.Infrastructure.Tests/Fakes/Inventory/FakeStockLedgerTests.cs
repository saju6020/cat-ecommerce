using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Fakes.Inventory;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Tests.Fakes.Inventory;

/// <summary>
/// FakeStockLedger must fail closed: no consumer may build on stock existing before HO-IMP-001
/// lands (docs/handover/CONTRACTS.md).
/// </summary>
public sealed class FakeStockLedgerTests
{
    private readonly FakeStockLedger _sut = new();

    private static readonly StockDemand Demand = new(
        BranchId: Guid.NewGuid(),
        SkuId: Guid.NewGuid(),
        Quantity: 1m,
        Purpose: MovementKind.Sale,
        LocationIds: null,
        ExplicitLines: null,
        OverrideReason: null);

    private static readonly StockReference Reference = new(StockReferenceKind.CustomerOrder, Guid.NewGuid(), null, null);
    private static readonly StockParty Party = new(Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task PlanAsync_fails_closed()
    {
        var result = await _sut.PlanAsync(Demand, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("InsufficientStock", result.ViolationCode);
    }

    [Fact]
    public async Task ReserveAsync_fails_closed()
    {
        var result = await _sut.ReserveAsync(Demand, Reference, Party, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("InsufficientStock", result.ViolationCode);
    }

    [Fact]
    public async Task ReleaseAsync_fails_closed()
    {
        var result = await _sut.ReleaseAsync(Reference, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ConsumeReservationAsync_fails_closed()
    {
        var result = await _sut.ConsumeReservationAsync(Reference, Party, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task DeductAsync_fails_closed()
    {
        var result = await _sut.DeductAsync(Demand, Reference, Party, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ReceiveReturnAsync_fails_closed()
    {
        var stockReturn = new StockReturn(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m, null);

        var result = await _sut.ReceiveReturnAsync(stockReturn, Reference, Party, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task AdjustAsync_fails_closed()
    {
        var adjustment = new StockAdjustment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m, "correction", null);

        var result = await _sut.AdjustAsync(adjustment, Reference, Party, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void RecordShortfall_does_not_throw()
    {
        var shortfall = new StockShortfall(Guid.NewGuid(), Guid.NewGuid(), 1m, Guid.NewGuid(), null);

        var exception = Record.Exception(() => _sut.RecordShortfall(shortfall));

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetBranchBalancesAsync_succeeds_with_no_balances()
    {
        var result = await _sut.GetBranchBalancesAsync(Guid.NewGuid(), [Guid.NewGuid()], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetDispensaryBatchesAsync_succeeds_with_no_batches()
    {
        var result = await _sut.GetDispensaryBatchesAsync(Guid.NewGuid(), [Guid.NewGuid()], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetClinicDispensaryAsync_fails_closed_because_no_dispensary_exists_yet()
    {
        var result = await _sut.GetClinicDispensaryAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("DispensaryNotFound", result.ViolationCode);
    }
}
