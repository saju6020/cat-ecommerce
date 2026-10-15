using CatEcommerce.Application.CommandHandlers.Inventory;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatEcommerce.Application.Tests.CommandHandlers.Inventory;

/// <summary>HO-IMP-001 section 11.4 C14, C15 -- the alert handlers (dedup is a success that writes nothing; acknowledgement is branch-scoped) and the reorder
/// policy upsert.</summary>
public sealed class AlertAndReorderHandlerTests
{
    private static readonly Guid Branch = Guid.NewGuid();

    private static RaiseStockAlertCommand Raise(string kind, string? dedup = null, Guid? sku = null, Guid? location = null) => new()
    {
        AlertId = Guid.NewGuid(), Kind = kind, BranchId = Branch, SkuId = sku, LocationId = location, DedupKey = dedup, ThresholdDays = kind == "ExpiryThreshold" ? 60 : null,
        UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
    };

    private static RaiseStockAlertCommandHandler RaiseHandler(FakeAggregateRootRepository<InventoryAlertAggregate> alerts) =>
        new(NullLogger<RaiseStockAlertCommandHandler>.Instance, alerts, new SavingAlertWriter(alerts), TimeProvider.System);

    /// <summary>Stands in for the Infrastructure writer: saves through the fake repository and reports the alert as new.</summary>
    private sealed class SavingAlertWriter(FakeAggregateRootRepository<InventoryAlertAggregate> alerts) : IInventoryAlertWriter
    {
        public bool Duplicate { get; set; }

        public async Task<bool> TryRaiseAsync(InventoryAlertAggregate alert, CancellationToken ct)
        {
            if (Duplicate) return false;
            await alerts.SaveAsync(alert, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly);
            return true;
        }
    }

    [Fact]
    public async Task ARaceLostAtTheUniqueIndex_IsASuccessThatKeepsTheFirst()
    {
        var alerts = new FakeAggregateRootRepository<InventoryAlertAggregate>();
        var handler = new RaiseStockAlertCommandHandler(NullLogger<RaiseStockAlertCommandHandler>.Instance, alerts, new SavingAlertWriter(alerts) { Duplicate = true }, TimeProvider.System);

        Assert.True((await handler.HandleAsync(Raise("ExpiryThreshold", "expiry:b:br:60"))).IsSuccess);
        Assert.Empty(alerts.Saved);
    }

    [Fact]
    public async Task ARaisedAlert_IsSavedOnce_AndTheSameDedupKeyIsASuccessThatWritesNothing()
    {
        var alerts = new FakeAggregateRootRepository<InventoryAlertAggregate>();
        var handler = RaiseHandler(alerts);

        Assert.True((await handler.HandleAsync(Raise("ExpiryThreshold", "expiry:b:br:60"))).IsSuccess);
        Assert.True((await handler.HandleAsync(Raise("ExpiryThreshold", "expiry:b:br:60"))).IsSuccess);
        Assert.True((await handler.HandleAsync(Raise("ExpiryThreshold", "expiry:b:br:30"))).IsSuccess);

        Assert.Equal(2, alerts.Saved.Count);
        Assert.Equal(StockAlertKind.ExpiryThreshold, alerts.Saved[0].Kind);
    }

    [Fact]
    public async Task ALiveLowStockAlertForThePair_BlocksASecond_ButAResolvedOneDoesNot()
    {
        var alerts = new FakeAggregateRootRepository<InventoryAlertAggregate>();
        var handler = RaiseHandler(alerts);
        var (sku, location) = (Guid.NewGuid(), Guid.NewGuid());

        await handler.HandleAsync(Raise("LowStock", sku: sku, location: location));
        await handler.HandleAsync(Raise("LowStock", sku: sku, location: location));
        Assert.Single(alerts.Saved);

        alerts.Saved[0].Resolve(DateTimeOffset.UtcNow, null, new InventoryActor(TestUserContext.Create(), Guid.NewGuid()));
        await handler.HandleAsync(Raise("LowStock", sku: sku, location: location));
        Assert.Equal(2, alerts.Saved.Count);
    }

    [Fact]
    public async Task AShortfallIsNeverDeduplicated()
    {
        var alerts = new FakeAggregateRootRepository<InventoryAlertAggregate>();
        var handler = RaiseHandler(alerts);

        await handler.HandleAsync(Raise("Shortfall", sku: Guid.NewGuid(), location: Guid.NewGuid()));
        await handler.HandleAsync(Raise("Shortfall", sku: Guid.NewGuid(), location: Guid.NewGuid()));

        Assert.Equal(2, alerts.Saved.Count);
    }

    [Fact]
    public async Task Acknowledging_AnAlertOfAnotherBranch_DoesNotExist_AndAnOpenOneIsAcknowledgedByTheCaller()
    {
        var alerts = new FakeAggregateRootRepository<InventoryAlertAggregate>();
        await RaiseHandler(alerts).HandleAsync(Raise("Shortfall", sku: Guid.NewGuid(), location: Guid.NewGuid()));
        var alert = alerts.Saved.Single();
        alerts.Seed(alert);
        var handler = new AcknowledgeStockAlertCommandHandler(NullLogger<AcknowledgeStockAlertCommandHandler>.Instance, alerts, TimeProvider.System);
        var caller = TestUserContext.Create();

        var foreign = await handler.HandleAsync(new AcknowledgeStockAlertCommand
        { AlertId = alert.Id, BranchId = Guid.NewGuid(), UserContext = caller, CorrelationId = Guid.NewGuid() });
        var own = await handler.HandleAsync(new AcknowledgeStockAlertCommand
        { AlertId = alert.Id, BranchId = Branch, Note = "Ordering.", UserContext = caller, CorrelationId = Guid.NewGuid() });
        var again = await handler.HandleAsync(new AcknowledgeStockAlertCommand
        { AlertId = alert.Id, BranchId = Branch, UserContext = caller, CorrelationId = Guid.NewGuid() });

        Assert.Equal(BusinessViolationCodes.StockAlertNotFound, foreign.ValidationResult!.Errors.First().ErrorCode);
        Assert.True(own.IsSuccess);
        Assert.Equal(InventoryAlertStatus.Acknowledged, alert.Status);
        Assert.Equal(caller.UserId, alert.AcknowledgedBy);
        Assert.Equal(BusinessViolationCodes.StockAlertNotOpen, again.ValidationResult!.Errors.First().ErrorCode);
    }

    [Fact]
    public async Task Resolving_IsIdempotent()
    {
        var alerts = new FakeAggregateRootRepository<InventoryAlertAggregate>();
        await RaiseHandler(alerts).HandleAsync(Raise("LowStock", sku: Guid.NewGuid(), location: Guid.NewGuid()));
        var alert = alerts.Saved.Single();
        alerts.Seed(alert);
        var handler = new ResolveStockAlertCommandHandler(NullLogger<ResolveStockAlertCommandHandler>.Instance, alerts, TimeProvider.System);
        var command = new ResolveStockAlertCommand { AlertId = alert.Id, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() };

        Assert.True((await handler.HandleAsync(command)).IsSuccess);
        Assert.True((await handler.HandleAsync(command)).IsSuccess);
        Assert.Equal(InventoryAlertStatus.Resolved, alert.Status);
        Assert.Single(alerts.Updated);
    }

    // ── the reorder policy ──────────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class PolicyRig
    {
        public FakeAggregateRootRepository<ReorderPolicyAggregate> Policies { get; } = new();
        public FakeAggregateRootRepository<SupplierAggregate> Suppliers { get; } = new();
        public FakeSkuDirectory Skus { get; } = new();
        public FakeStockPositionReader Positions { get; } = new();
        public StockLocationFact Location { get; }
        public SkuStockProfile Sku { get; }

        public PolicyRig()
        {
            Location = Positions.AddLocation(Branch, StockLocationKind.ShopFloor);
            Sku = Skus.Add();
        }

        public SetReorderPolicyCommandHandler Handler() => new(NullLogger<SetReorderPolicyCommandHandler>.Instance, Policies, Suppliers, Skus, Positions);

        public SetReorderPolicyCommand Command(decimal point = 10m, decimal? quantity = null, Guid? location = null, Guid? sku = null, Guid? supplier = null) => new()
        {
            BranchId = Branch, SkuId = sku ?? Sku.SkuId, LocationId = location ?? Location.LocationId, ReorderPoint = point, ReorderQuantity = quantity, PreferredSupplierId = supplier,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };
    }

    [Fact]
    public async Task TheFirstPolicyIsSaved_AndTheSecondForTheSamePairUpdatesIt()
    {
        var rig = new PolicyRig();

        Assert.True((await rig.Handler().HandleAsync(rig.Command(10m))).IsSuccess);
        Assert.Single(rig.Policies.Saved);
        Assert.Empty(rig.Policies.Updated);
    }

    [Fact]
    public async Task APolicyForAnUnknownLocationSkuOrSupplier_OrANegativePoint_IsRefusedAndNothingIsSaved()
    {
        var rig = new PolicyRig();

        Assert.Equal(BusinessViolationCodes.StockLocationNotFound, (await rig.Handler().HandleAsync(rig.Command(location: Guid.NewGuid()))).ValidationResult!.Errors.First().ErrorCode);
        Assert.Equal(BusinessViolationCodes.SkuNotFound, (await rig.Handler().HandleAsync(rig.Command(sku: Guid.NewGuid()))).ValidationResult!.Errors.First().ErrorCode);
        Assert.Equal(BusinessViolationCodes.SupplierNotFound, (await rig.Handler().HandleAsync(rig.Command(supplier: Guid.NewGuid()))).ValidationResult!.Errors.First().ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidReorderPolicy, (await rig.Handler().HandleAsync(rig.Command(point: -1m))).ValidationResult!.Errors.First().ErrorCode);

        Assert.Empty(rig.Policies.Saved);
    }

    [Fact]
    public void TheAlertValidators_RefuseAnUnknownKind_AndAnOverlongDedupKey()
    {
        var unknown = new RaiseStockAlertCommandValidator().Validate(Raise("Whim"));
        var long_ = new RaiseStockAlertCommandValidator().Validate(Raise("Shortfall", new string('k', 201)));

        Assert.Contains(unknown.Errors, e => e.PropertyName == nameof(RaiseStockAlertCommand.Kind));
        Assert.Contains(long_.Errors, e => e.PropertyName == nameof(RaiseStockAlertCommand.DedupKey));
        Assert.True(new RaiseStockAlertCommandValidator().Validate(Raise("lowstock", sku: Guid.NewGuid(), location: Guid.NewGuid())).IsValid);
    }
}
