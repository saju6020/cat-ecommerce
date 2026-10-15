using CatEcommerce.Application.CommandHandlers.Order;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Order;

/// <summary>
/// HO-ORD-001 §8.9 (ADR-208), U-D11 -- Ordering's whole relationship with stock: every <c>IStockLedger</c> call carries the reference
/// (<c>CustomerOrder</c>, order, line) and the party (customer, cat) that Inventory's recall trace reads back (US-IMP-11 AC2); demands go out in SKU
/// order; a repeated SKU flushes between plans; Ordering never chooses a batch (it only records what the ledger returned).
/// </summary>
public sealed class OrderStockTests
{
    private sealed class RecordingLedger(Func<StockDemand, StockResult<StockPlan>>? answer = null) : IStockLedger
    {
        public List<(StockDemand Demand, StockReference Reference, StockParty Party)> Reserves { get; } = [];

        public Task<StockResult<StockPlan>> ReserveAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct)
        {
            Reserves.Add((demand, reference, party));
            return Task.FromResult(answer?.Invoke(demand) ?? StockResult<StockPlan>.Success(
                new StockPlan([new StockMovementLine(Guid.NewGuid(), "LOT-" + demand.SkuId.ToString("N")[..4], null, Guid.NewGuid(), demand.Quantity, false)], 0m)));
        }

        public Task<StockResult<StockPlan>> PlanAsync(StockDemand demand, CancellationToken ct) => throw new NotSupportedException();
        public List<StockReference> Releases { get; } = [];
        public Task<StockResult<Unit>> ReleaseAsync(StockReference reference, CancellationToken ct) { Releases.Add(reference); return Task.FromResult(StockResult<Unit>.Success(Unit.Value)); }
        public Task<StockResult<IReadOnlyList<StockMovementLine>>> ConsumeReservationAsync(StockReference reference, StockParty party, CancellationToken ct) => throw new NotSupportedException();
        public Task<StockResult<IReadOnlyList<StockMovementLine>>> DeductAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct) => throw new NotSupportedException();
        public Task<StockResult<IReadOnlyList<StockMovementLine>>> ReceiveReturnAsync(StockReturn stockReturn, StockReference reference, StockParty party, CancellationToken ct) => throw new NotSupportedException();
        public List<(StockBatchDemand Demand, StockReference Reference, StockParty Party)> BatchReserves { get; } = [];
        public Func<IReadOnlyCollection<Guid>, IReadOnlyList<BatchUsability>> Usability { get; init; } = ids => ids.Select(i => new BatchUsability(i, Guid.Empty, "LOT", null, null)).ToList();
        public Task<StockResult<StockPlan>> ReserveBatchAsync(StockBatchDemand demand, StockReference reference, StockParty party, CancellationToken ct)
        {
            BatchReserves.Add((demand, reference, party));
            return Task.FromResult(StockResult<StockPlan>.Success(new StockPlan([new StockMovementLine(demand.BatchId, "CHOSEN", null, Guid.NewGuid(), demand.Quantity, true)], 0m)));
        }
        public Task<StockResult<IReadOnlyList<BatchUsability>>> GetBatchUsabilityAsync(Guid branchId, IReadOnlyCollection<Guid> batchIds, CancellationToken ct) =>
            Task.FromResult(StockResult<IReadOnlyList<BatchUsability>>.Success(Usability(batchIds)));
        public void RecordShortfall(StockShortfall shortfall) => throw new NotSupportedException();
        public Task<StockResult<IReadOnlyList<StockMovementLine>>> AdjustAsync(StockAdjustment adjustment, StockReference reference, StockParty party, CancellationToken ct) => throw new NotSupportedException();
        public Task<StockResult<IReadOnlyList<BatchBalance>>> GetBranchBalancesAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<StockResult<IReadOnlyList<DispensaryBatch>>> GetDispensaryBatchesAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<StockResult<DispensaryLocation>> GetClinicDispensaryAsync(Guid branchId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class RecordingScope : ITransactionalScope
    {
        public List<string> Events { get; } = [];
        public Task<T> RunAsync<T>(Func<Task<T>> work, Func<T, bool> commitWhen, CancellationToken ct = default) => work();
        public Task FlushAsync(CancellationToken ct = default) { Events.Add("flush"); return Task.CompletedTask; }
    }

    private static readonly UserContext Customer = new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Customer"] };

    private static OrderLineDto Line(int number, Guid skuId, int quantity, Guid? catId = null, bool bundle = false, IReadOnlyList<OrderLineComponent>? components = null, string name = "Product") =>
        new(Guid.NewGuid(), number, skuId, Guid.NewGuid(), name, name, null, ProductReturnClass.Other, bundle, catId, quantity, 100m, 100m * quantity, 0m, 0m, Guid.NewGuid(), 0m, 0m, 100m * quantity, components ?? []);

    private static OrderAggregate Order(params OrderLineDto[] lines)
    {
        var subtotal = lines.Sum(l => l.LineSubtotal);
        var order = new OrderAggregate(Guid.NewGuid());
        order.Place(new PlaceOrderDto(
            "ORD-2026-000001", OrderChannel.Online, Customer.UserId, Guid.NewGuid(), FulfilmentMethod.Delivery, OrderPaymentMethod.CashOnDelivery, "BDT",
            new FulfilmentDetails("Rina", "01712345678", "House 1", null, "Dhaka", "Dhaka", "1212", null, null, null, null, null, null, null, null, null),
            lines, new OrderTotals(subtotal, 0m, 0m, 0m, 0m, subtotal), null, null, new string('a', 64), Guid.NewGuid(), DateTimeOffset.UtcNow, Customer, Guid.NewGuid()));
        return order;
    }

    [Fact]
    public async Task U_D11_EveryReserveCall_CarriesTheOrderReferenceAndThePartyStamp()
    {
        var cat = Guid.NewGuid();
        var order = Order(Line(1, Guid.NewGuid(), 2, cat), Line(2, Guid.NewGuid(), 1));
        var ledger = new RecordingLedger();

        var outcome = await OrderStock.ReserveAsync(ledger, new RecordingScope(), order);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(2, ledger.Reserves.Count);
        foreach (var (demand, reference, party) in ledger.Reserves)
        {
            var line = order.Lines.Single(l => l.Id == reference.ReferenceLineId);
            Assert.Equal(StockReferenceKind.CustomerOrder, reference.ReferenceKind);
            Assert.Equal(order.Id, reference.ReferenceId);
            Assert.Equal(Customer.UserId, party.CustomerId);
            Assert.Equal(line.CatId, party.CatId);
            Assert.Equal(MovementKind.Sale, demand.Purpose);
            Assert.Equal(order.BranchId, demand.BranchId);
            Assert.Equal(line.Quantity, demand.Quantity);
        }

        // what Inventory chose is recorded on the lines -- Ordering picked no batch
        Assert.Equal(2, outcome.Allocations.Count);
        Assert.All(outcome.Allocations, a => Assert.Contains(order.Lines, l => l.Id == a.LineId));
    }

    [Fact]
    public async Task ABundle_ReservesEachComponent_TimesTheQuantity_UnderTheBundlesLine()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var order = Order(Line(1, Guid.NewGuid(), 3, bundle: true, components: [new OrderLineComponent(a, 2), new OrderLineComponent(b, 1)]));
        var ledger = new RecordingLedger();

        var outcome = await OrderStock.ReserveAsync(ledger, new RecordingScope(), order);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(6m, ledger.Reserves.Single(r => r.Demand.SkuId == a).Demand.Quantity);
        Assert.Equal(3m, ledger.Reserves.Single(r => r.Demand.SkuId == b).Demand.Quantity);
        Assert.All(ledger.Reserves, r => Assert.Equal(order.Lines[0].Id, r.Reference.ReferenceLineId));
        Assert.Equal(new[] { a, b }.ToHashSet(), outcome.Allocations.Select(x => x.SkuId).ToHashSet());
    }

    [Fact]
    public async Task Demands_GoOut_InSkuOrder_SoConcurrentPlacementsQueueInsteadOfDeadlocking()
    {
        var low = new Guid("00000000-0000-0000-0000-000000000001");
        var high = new Guid("ffffffff-0000-0000-0000-000000000001");
        var order = Order(Line(1, high, 1), Line(2, low, 1));
        var ledger = new RecordingLedger();

        await OrderStock.ReserveAsync(ledger, new RecordingScope(), order);

        Assert.Equal([low, high], ledger.Reserves.Select(r => r.Demand.SkuId));
    }

    [Fact]
    public async Task TwoLinesOfOneSku_FlushBetweenThePlans_SoTheSecondPlanSeesTheFirst()
    {
        var sku = Guid.NewGuid();
        var order = Order(Line(1, sku, 1, Guid.NewGuid()), Line(2, sku, 1, Guid.NewGuid()));
        var scope = new RecordingScope();

        await OrderStock.ReserveAsync(new RecordingLedger(), scope, order);

        Assert.Single(scope.Events);
    }

    [Fact]
    public async Task InsufficientStock_IsStockNoLongerAvailable_NamingTheProduct()
    {
        var order = Order(Line(1, Guid.NewGuid(), 1, name: "Royal Canin Kitten 2 kg"));
        var ledger = new RecordingLedger(_ => StockResult<StockPlan>.Failure(BusinessViolationCodes.InsufficientStock, "Not enough stock.", "Quantity"));

        var outcome = await OrderStock.ReserveAsync(ledger, new RecordingScope(), order);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(BusinessViolationCodes.StockNoLongerAvailable, outcome.ViolationCode);
        Assert.Contains("Royal Canin Kitten 2 kg", outcome.Message);
    }

    [Fact]
    public void ThePlaceOrderValidator_AcceptsAWellFormedCommand_AndRefusesBadShapes()
    {
        PlaceOrderCommand Command(Action<Dictionary<string, object?>>? tweak = null)
        {
            var values = new Dictionary<string, object?> { ["fp"] = new string('a', 64), ["method"] = FulfilmentMethod.Delivery, ["payment"] = OrderPaymentMethod.CashOnDelivery, ["code"] = "SAVE10" };
            tweak?.Invoke(values);
            return new PlaceOrderCommand
            {
                OrderId = Guid.NewGuid(), IdempotencyKey = Guid.NewGuid(), FulfilmentMethod = (FulfilmentMethod)values["method"]!, PaymentMethod = (OrderPaymentMethod)values["payment"]!,
                DiscountCode = (string?)values["code"], QuoteFingerprint = (string)values["fp"]!, UserContext = Customer, CorrelationId = Guid.NewGuid(),
            };
        }

        var validator = new PlaceOrderCommandValidator();
        Assert.True(validator.Validate(Command()).IsValid);
        Assert.False(validator.Validate(Command(v => v["fp"] = "not-a-fingerprint")).IsValid);
        Assert.False(validator.Validate(Command(v => v["fp"] = new string('A', 64))).IsValid);   // lower-case hex only
        Assert.False(validator.Validate(Command(v => v["method"] = FulfilmentMethod.Counter)).IsValid);
        Assert.False(validator.Validate(Command(v => v["payment"] = OrderPaymentMethod.InStore)).IsValid);
        Assert.False(validator.Validate(Command(v => v["code"] = new string('x', 41))).IsValid);
    }

    // ── owner decision 2 (2026-10-09): fulfilment reads and writes stock only through the published IStockLedger ──

    private static (OrderAggregate Order, Guid LineId, Guid Sku, Guid Batch) AllocatedOrder()
    {
        var sku = Guid.NewGuid();
        var order = Order(Line(1, sku, 2, name: "Royal Canin Kitten 2 kg"));
        var line = order.Lines[0];
        var batch = Guid.NewGuid();
        order.RecordAllocations([new AllocationDto(line.Id, sku, batch, "LOT-1", Guid.NewGuid(), 2m, null)]);
        return (order, line.Id, sku, batch);
    }

    [Fact]
    public async Task BR14_ARecalledBatch_IsNamedBeforeTheOrderLeaves_ByInventorysOwnVerdict()
    {
        var (order, _, _, batch) = AllocatedOrder();
        var ledger = new RecordingLedger { Usability = ids => ids.Select(i => new BatchUsability(i, Guid.Empty, "LOT-1", BusinessViolationCodes.BatchRecalled, "recalled")).ToList() };

        var refusal = await OrderStockFulfilment.CheckBatchesAsync(ledger, order);

        Assert.NotNull(refusal);
        Assert.Equal(BusinessViolationCodes.BatchRecalled, refusal!.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Contains("Royal Canin Kitten 2 kg", refusal.ValidationResult.Errors.Single().ErrorMessage);
        Assert.NotEqual(Guid.Empty, batch);
    }

    [Theory]
    [InlineData(BusinessViolationCodes.BatchExpired)]
    [InlineData(BusinessViolationCodes.BatchNotUsable)]
    public async Task BR14_AnExpiredOrWithdrawnBatch_KeepsItsOwnCode(string code)
    {
        var (order, _, _, _) = AllocatedOrder();
        var ledger = new RecordingLedger { Usability = ids => ids.Select(i => new BatchUsability(i, Guid.Empty, "LOT-1", code, "x")).ToList() };

        var refusal = await OrderStockFulfilment.CheckBatchesAsync(ledger, order);

        Assert.Equal(code, refusal!.ValidationResult!.Errors.Single().ErrorCode);
    }

    [Fact]
    public async Task BR14_UsableBatches_PassWithoutAnyReservation()
    {
        var (order, _, _, _) = AllocatedOrder();
        var ledger = new RecordingLedger();

        Assert.Null(await OrderStockFulfilment.CheckBatchesAsync(ledger, order));
        Assert.Empty(ledger.Reserves);
        Assert.Empty(ledger.BatchReserves);
    }

    [Fact]
    public async Task Reallocation_NamesOnlyTheStaffChosenBatch_InventoryPicksTheLocations()
    {
        var (order, lineId, sku, _) = AllocatedOrder();
        var target = Guid.NewGuid();
        var ledger = new RecordingLedger { Usability = ids => ids.Select(i => new BatchUsability(i, sku, "LOT-9", null, null)).ToList() };
        var scope = new RecordingScope();

        var (allocations, refusal) = await OrderStockFulfilment.ReallocateAsync(ledger, scope, order, order.Lines.Single(l => l.Id == lineId), target, "Closest expiry was damaged");

        Assert.Null(refusal);
        var call = Assert.Single(ledger.BatchReserves);
        Assert.Equal(target, call.Demand.BatchId);
        Assert.Equal(sku, call.Demand.SkuId);
        Assert.Equal(2m, call.Demand.Quantity);
        Assert.Equal("Closest expiry was damaged", call.Demand.Reason);
        Assert.Equal(lineId, call.Reference.ReferenceLineId);
        Assert.Single(ledger.Releases);                // the old reservation was released first
        Assert.Single(scope.Events);                   // and flushed so the second plan sees it
        Assert.Empty(ledger.Reserves);                 // no FEFO demand, no explicit location lines from Ordering
        Assert.Equal(target, Assert.Single(allocations!).BatchId);
    }

    [Fact]
    public async Task Reallocation_ToABatchOfAnotherItemOrBranch_IsBatchNotFound_AndTouchesNothing()
    {
        var (order, lineId, _, _) = AllocatedOrder();
        var otherItem = new RecordingLedger { Usability = ids => ids.Select(i => new BatchUsability(i, Guid.NewGuid(), "LOT-9", null, null)).ToList() };
        var elsewhere = new RecordingLedger { Usability = ids => ids.Select(i => new BatchUsability(i, Guid.Empty, string.Empty, BusinessViolationCodes.BatchNotFound, "no")).ToList() };

        foreach (var ledger in new[] { otherItem, elsewhere })
        {
            var (allocations, refusal) = await OrderStockFulfilment.ReallocateAsync(ledger, new RecordingScope(), order, order.Lines.Single(l => l.Id == lineId), Guid.NewGuid(), "A good reason");

            Assert.Null(allocations);
            Assert.Equal(BusinessViolationCodes.BatchNotFound, refusal!.ValidationResult!.Errors.Single().ErrorCode);
            Assert.Empty(ledger.Releases);
            Assert.Empty(ledger.BatchReserves);
        }
    }

    [Fact]
    public async Task Reallocation_OfABundle_ReservesTheChosenComponentByBatch_AndTheOthersFefo()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var order = Order(Line(1, Guid.NewGuid(), 1, bundle: true, components: [new OrderLineComponent(a, 1), new OrderLineComponent(b, 2)]));
        var line = order.Lines[0];
        var ledger = new RecordingLedger { Usability = ids => ids.Select(i => new BatchUsability(i, b, "LOT-B", null, null)).ToList() };

        var (allocations, refusal) = await OrderStockFulfilment.ReallocateAsync(ledger, new RecordingScope(), order, line, Guid.NewGuid(), "Reason given here");

        Assert.Null(refusal);
        Assert.Equal(b, Assert.Single(ledger.BatchReserves).Demand.SkuId);
        var fefo = Assert.Single(ledger.Reserves);
        Assert.Equal(a, fefo.Demand.SkuId);
        Assert.Null(fefo.Demand.ExplicitLines);
        Assert.Equal(2, allocations!.Count);
    }
}
