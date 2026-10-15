using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 §7 V3 / §20.2 (I-A3) -- the upstream ports Ordering relies on, checked against the REAL implementations on master and recorded as tests. This is
/// the stock half (<c>IStockLedger</c>, HO-IMP-001: enlists without saving, FEFO, the last-unit race) and the executable record of the Billing finding: the
/// enlisting invoice, payment and credit-note ports §8.14 assumed do not exist (Billing published a charge recorder and commands).
/// </summary>
public sealed class OrderingStockContractEndpointTests(OrderingApiFactory factory) : OrderingIntegrationTestBase(factory)
{
    private static StockDemand Demand(Guid branchId, Guid skuId, decimal quantity) => new(branchId, skuId, quantity, MovementKind.Sale, null, null, null);

    private static StockReference OrderReference(Guid orderId, Guid? lineId = null) => new(StockReferenceKind.CustomerOrder, orderId, lineId ?? Guid.NewGuid(), "Order " + orderId.ToString("N")[..6]);

    private async Task<decimal> SellableAsync(Guid branchId, Guid skuId) =>
        await WithDbAsync(db => db.SkuSellable.AsNoTracking().Where(v => v.SkuId == skuId && v.BranchId == branchId).Select(v => v.SellableQuantity).SumAsync());

    private sealed class Port : IAsyncDisposable
    {
        private readonly IServiceScope _scope;

        public Port(IServiceScope scope)
        {
            _scope = scope;
            Ledger = scope.ServiceProvider.GetRequiredService<IStockLedger>();
            Db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        }

        public IStockLedger Ledger { get; }
        public CatEcommerceDbContext Db { get; }
        public int Enlisted => Db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added);
        public ValueTask DisposeAsync() { _scope.Dispose(); return ValueTask.CompletedTask; }
    }

    private Port OpenPort() => new(Factory.PortHost.Services.CreateScope());

    [Fact]
    public async Task V3_Reserve_EnlistsWithoutSaving_FefoPicksTheBatch_AndTheSellableQuantityDropsWhenTheCallerCommits()
    {
        var context = await CreateContextAsync();
        var sku = await CreateSkuAsync(context, stock: 5m);
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        await using var port = OpenPort();
        var result = await port.Ledger.ReserveAsync(Demand(context.BranchId, sku.SkuId, 2m), OrderReference(orderId), new StockParty(customerId, catId), default);

        Assert.True(result.IsSuccess, result.Message);
        var line = Assert.Single(result.Value!.Lines);
        Assert.Equal(sku.BatchIds[0], line.BatchId);
        Assert.Equal(context.ShopLocationId, line.LocationId);
        Assert.Equal(2m, line.Quantity);
        Assert.True(port.Enlisted > 0);                                  // staged on the caller's context ...
        Assert.Equal(5m, await SellableAsync(context.BranchId, sku.SkuId)); // ... and not visible until the caller's own save

        await port.Db.SaveChangesAsync();
        Assert.Equal(3m, await SellableAsync(context.BranchId, sku.SkuId));

        // the party and reference stamp is written on the entry (US-IMP-11 AC2, D-4)
        var entry = await WithDbAsync(db => db.StockReservationEntries.AsNoTracking().Where(e => e.ReferenceId == orderId)
            .Select(e => new { e.CustomerId, e.CatId, e.BatchId, e.QuantityDelta, e.ReferenceKind }).SingleAsync());
        Assert.Equal(customerId, entry.CustomerId);
        Assert.Equal(catId, entry.CatId);
        Assert.Equal(StockReferenceKind.CustomerOrder, entry.ReferenceKind);
        Assert.Equal(2m, entry.QuantityDelta);
    }

    [Fact]
    public async Task V3_Release_GivesTheStockBack_AndConsume_TurnsTheReservationIntoTheSale()
    {
        var context = await CreateContextAsync();
        var sku = await CreateSkuAsync(context, stock: 5m);
        var released = Guid.NewGuid();
        var consumed = Guid.NewGuid();
        var party = new StockParty(Guid.NewGuid(), null);

        await using (var reserve = OpenPort())
        {
            Assert.True((await reserve.Ledger.ReserveAsync(Demand(context.BranchId, sku.SkuId, 2m), OrderReference(released), party, default)).IsSuccess);
            Assert.True((await reserve.Ledger.ReserveAsync(Demand(context.BranchId, sku.SkuId, 1m), OrderReference(consumed), party, default)).IsSuccess);
            await reserve.Db.SaveChangesAsync();
        }

        Assert.Equal(2m, await SellableAsync(context.BranchId, sku.SkuId));

        await using (var release = OpenPort())
        {
            Assert.True((await release.Ledger.ReleaseAsync(new StockReference(StockReferenceKind.CustomerOrder, released, null, null), default)).IsSuccess);
            await release.Db.SaveChangesAsync();
        }

        Assert.Equal(4m, await SellableAsync(context.BranchId, sku.SkuId));

        await using (var consume = OpenPort())
        {
            var result = await consume.Ledger.ConsumeReservationAsync(new StockReference(StockReferenceKind.CustomerOrder, consumed, null, null), party, default);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(1m, result.Value!.Sum(l => l.Quantity));
            await consume.Db.SaveChangesAsync();
        }

        Assert.Equal(4m, await SellableAsync(context.BranchId, sku.SkuId)); // the sold unit left on-hand and left the reservation: sellable is unchanged
    }

    [Fact]
    public async Task V3_Fefo_TheBatchThatExpiresFirstIsReservedFirst()
    {
        var context = await CreateContextAsync();
        var sku = await CreateSkuAsync(context, stock: 0m, food: true);
        var later = await ReceiveAsync(context.Admin, sku.SkuId, context.ShopLocationId, 3m, UtcToday.AddDays(300));
        var sooner = await ReceiveAsync(context.Admin, sku.SkuId, context.ShopLocationId, 3m, UtcToday.AddDays(100));

        await using var port = OpenPort();
        var result = await port.Ledger.ReserveAsync(Demand(context.BranchId, sku.SkuId, 4m), OrderReference(Guid.NewGuid()), new StockParty(Guid.NewGuid(), null), default);

        Assert.True(result.IsSuccess, result.Message);
        var lines = result.Value!.Lines;
        Assert.Equal([sooner, later], lines.Select(l => l.BatchId));
        Assert.Equal([3m, 1m], lines.Select(l => l.Quantity));
        Assert.Equal(UtcToday.AddDays(100), lines[0].ExpiresOn);
        Assert.False(string.IsNullOrWhiteSpace(lines[0].LotNumber));
    }

    [Fact]
    public async Task V3_ReservingMoreThanIsSellable_IsInsufficientStock_NotAnException_AndEnlistsNothing()
    {
        var context = await CreateContextAsync();
        var sku = await CreateSkuAsync(context, stock: 5m);

        await using var port = OpenPort();
        var result = await port.Ledger.ReserveAsync(Demand(context.BranchId, sku.SkuId, 10m), OrderReference(Guid.NewGuid()), new StockParty(Guid.NewGuid(), null), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessViolationCodes.InsufficientStock, result.ViolationCode);
        Assert.Equal(0, port.Enlisted);
    }

    /// <summary>The mechanism behind I-D9 (US-IMP-08 AC2): twenty callers reserve the last unit; Inventory's projection CHECK (<c>ck_stock_position_balance</c>) lets exactly one
    /// commit. No retries, delays or serialisation in the test (§20.3).</summary>
    [Fact]
    public async Task V3_TwentyConcurrentReservationsOfTheLastUnit_ExactlyOneCommits()
    {
        var context = await CreateContextAsync();
        var sku = await CreateSkuAsync(context, stock: 1m);

        async Task<string> TryAsync()
        {
            await using var port = OpenPort();
            var plan = await port.Ledger.ReserveAsync(Demand(context.BranchId, sku.SkuId, 1m), OrderReference(Guid.NewGuid()), new StockParty(Guid.NewGuid(), null), default);
            if (!plan.IsSuccess) return plan.ViolationCode!;

            try
            {
                await port.Db.SaveChangesAsync();
                return "committed";
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
            {
                return $"{pg.SqlState}:{pg.ConstraintName}";
            }
        }

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(TryAsync)));

        Assert.Equal(1, outcomes.Count(o => o == "committed"));
        Assert.All(outcomes.Where(o => o != "committed"), o => Assert.True(o is "InsufficientStock" or "23514:ck_stock_position_balance", $"unexpected outcome {o}"));
        Assert.Equal(0m, await SellableAsync(context.BranchId, sku.SkuId));
    }

    [Fact]
    public async Task V3_TheReservationStaysWithinTheBranch_AStockedOtherBranchDoesNotHelp()
    {
        var context = await CreateContextAsync();
        var other = await CreateContextAsync();
        var sku = await CreateSkuAsync(other, stock: 5m); // stock exists, but at another branch

        await using var port = OpenPort();
        var result = await port.Ledger.ReserveAsync(Demand(context.BranchId, sku.SkuId, 1m), OrderReference(Guid.NewGuid()), new StockParty(Guid.NewGuid(), null), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessViolationCodes.InsufficientStock, result.ViolationCode);
    }

    // ── the finding: what Billing actually published ────────────────────────────────────────────────
    /// <summary>
    /// HO-ORD-001 §20.2: §8.14 assumed three <b>enlisting</b> Billing ports (<c>IInvoiceIssuer</c>, <c>IPaymentRecorder</c>, <c>ICreditNoteIssuer</c>) that stage an invoice, a
    /// payment and a credit note on the caller's context without saving, which is what would make "order + reservation + invoice atomically" (US-ORD-08 AC1, I-D8)
    /// literally true. HO-BIL-001 built a different contract (its §8.4, §8.13): a charge recorder that enlists (<c>IBillableChargeRecorder</c>) and
    /// <b>commands</b> that save on their own (<c>IssueInvoiceFromChargesCommand</c>, <c>RecordPaymentCommand</c>, <c>IssueCreditNoteCommand</c>). This test fails the day
    /// Billing publishes the assumed ports, which is the signal to revisit HO-ORD-001 §1 Amended; until then it keeps the finding executable.
    /// </summary>
    [Fact]
    public void V3_BillingPublishesNoEnlistingInvoicePaymentOrCreditNotePort()
    {
        var domain = typeof(IStockLedger).Assembly;
        var names = domain.GetTypes().Select(t => t.Name).ToHashSet();

        Assert.DoesNotContain("IInvoiceIssuer", names);
        Assert.DoesNotContain("IPaymentRecorder", names);
        Assert.DoesNotContain("ICreditNoteIssuer", names);
        Assert.DoesNotContain("IStoreCreditLedger", names);

        // what does exist: the charge recorder (enlists), and the Billing commands (which commit their own save)
        Assert.Contains("IBillableChargeRecorder", names);
        var application = typeof(CatEcommerce.Application.Commands.Billing.RecordPaymentCommand).Assembly.GetTypes().Select(t => t.Name).ToHashSet();
        Assert.Contains("IssueInvoiceFromChargesCommand", application);
        Assert.Contains("RecordPaymentCommand", application);
        Assert.Contains("IssueCreditNoteCommand", application);
    }

    [Fact]
    public void V3_ThereIsNoCatalogueLookupNorBatchStatusPort_InShopOrInventory_SoOrderingReadsTheirPublishedProjections()
    {
        var names = typeof(IStockLedger).Assembly.GetTypes().Select(t => t.Name).Where(n => n is "IBatchStatusQuery").ToList();
        Assert.Empty(names);

        // ICatalogueLookup is Ordering's own read port (Infrastructure adapter over Shop's SkuRows/PriceRows), as Inventory's ISkuDirectory is.
        Assert.Contains("ICatalogueLookup", typeof(IStockLedger).Assembly.GetTypes().Select(t => t.Name));
    }
}
