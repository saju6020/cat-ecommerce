using CatEcommerce.Application.CommandHandlers.Inventory;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatEcommerce.Application.Tests.CommandHandlers.Inventory;

/// <summary>HO-IMP-001 §11.4 C3/C4 -- the handler-level checks: orchestration and "not found" marking, never the aggregate's own rules.</summary>
public sealed class ProcurementHandlerTests
{
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 7);

    private static CreateSupplierCommand NewSupplier(string code) => new()
    {
        SupplierId = Guid.NewGuid(), Code = code, Name = "Deutsche Feed", CountryCode = "DE", CurrencyCode = "EUR", LeadTimeDays = 21,
        PaymentTerms = "30 days", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
    };

    private static async Task<SupplierAggregate> ActiveSupplierAsync(FakeAggregateRootRepository<SupplierAggregate> repository, string code = "SUP-1")
    {
        var handler = new CreateSupplierCommandHandler(NullLogger<CreateSupplierCommandHandler>.Instance, repository);
        Assert.True((await handler.HandleAsync(NewSupplier(code))).IsSuccess);
        return repository.Saved.Single(s => s.Code == code);
    }

    [Fact]
    public async Task CreateSupplier_ACodeThatDiffersOnlyByCase_IsRefused()
    {
        var repository = new FakeAggregateRootRepository<SupplierAggregate>();
        await ActiveSupplierAsync(repository, "SUP-1");

        var response = await new CreateSupplierCommandHandler(NullLogger<CreateSupplierCommandHandler>.Instance, repository).HandleAsync(NewSupplier("sup-1"));

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.SupplierCodeAlreadyExists);
        Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task UpdateSupplier_AnUnknownSupplier_IsMarkedNotFoundSoTheEndpointAnswers404()
    {
        var response = await new UpdateSupplierCommandHandler(NullLogger<UpdateSupplierCommandHandler>.Instance, new FakeAggregateRootRepository<SupplierAggregate>())
            .HandleAsync(new UpdateSupplierCommand
            {
                SupplierId = Guid.NewGuid(), Name = "X", CountryCode = "DE", CurrencyCode = "EUR", LeadTimeDays = 1, PaymentTerms = "t",
                UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
            });

        var error = Assert.Single(response.ValidationResult!.Errors);
        Assert.Equal(BusinessViolationCodes.SupplierNotFound, error.ErrorCode);
        Assert.Equal(InventoryRules.NotFoundResource, error.ResourceName);
    }

    private sealed record Setup(
        CreatePurchaseOrderCommandHandler Handler, FakeAggregateRootRepository<PurchaseOrderAggregate> Orders, SupplierAggregate Supplier,
        FakeStockPositionReader Positions, FakeSkuDirectory Skus, StockLocationFact Warehouse);

    private static async Task<Setup> NewSetupAsync(bool supplierActive = true)
    {
        var suppliers = new FakeAggregateRootRepository<SupplierAggregate>();
        var supplier = await ActiveSupplierAsync(suppliers);
        if (!supplierActive) supplier.Deactivate(new InventoryActor(TestUserContext.Create(), Guid.NewGuid()));

        var positions = new FakeStockPositionReader();
        var warehouse = positions.AddLocation(Branch, StockLocationKind.Warehouse);
        var skus = new FakeSkuDirectory();
        var orders = new FakeAggregateRootRepository<PurchaseOrderAggregate>();

        return new Setup(
            new CreatePurchaseOrderCommandHandler(NullLogger<CreatePurchaseOrderCommandHandler>.Instance, orders, suppliers, positions, skus, new FakeDocumentNumbers()),
            orders, supplier, positions, skus, warehouse);
    }

    private static CreatePurchaseOrderCommand Order(Setup s, Guid locationId, params Guid[] skuIds) => new()
    {
        PurchaseOrderId = Guid.NewGuid(), BranchId = Branch, SupplierId = s.Supplier.Id, DeliverToLocationId = locationId, ExpectedDeliveryOn = Today.AddDays(7),
        Lines = skuIds.Select(id => new PurchaseOrderLineInput(Guid.NewGuid(), id, 10m, 2.5m)).ToList(),
        UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
    };

    [Fact]
    public async Task CreatePurchaseOrder_SnapshotsTheSkuAndDefaultsTheCurrencyToTheSuppliers()
    {
        var s = await NewSetupAsync();
        var sku = s.Skus.Add();

        var response = await s.Handler.HandleAsync(Order(s, s.Warehouse.LocationId, sku.SkuId));

        Assert.True(response.IsSuccess);
        var order = Assert.Single(s.Orders.Saved);
        Assert.Equal("EUR", order.CurrencyCode);
        Assert.Equal(sku.SkuCode, order.Lines.Single().SkuCode);
        Assert.StartsWith("PUR-2026-", (string)response.Result!);
    }

    [Fact]
    public async Task CreatePurchaseOrder_AnUnknownOrInactiveSku_IsRefusedOnTheLine()
    {
        var s = await NewSetupAsync();
        var inactive = s.Skus.Add(active: false);

        var unknown = await s.Handler.HandleAsync(Order(s, s.Warehouse.LocationId, Guid.NewGuid()));
        var off = await s.Handler.HandleAsync(Order(s, s.Warehouse.LocationId, inactive.SkuId));

        Assert.Contains(unknown.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.SkuNotFound && e.PropertyName == "Lines[0].SkuId");
        Assert.Contains(off.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.SkuInactive);
        Assert.Empty(s.Orders.Saved);
    }

    [Fact]
    public async Task CreatePurchaseOrder_ALocationThatCannotReceiveOrIsAnotherBranchs_IsRefused()
    {
        var s = await NewSetupAsync();
        var sku = s.Skus.Add();
        var elsewhere = s.Positions.AddLocation(Guid.NewGuid(), StockLocationKind.Warehouse);

        var foreign = await s.Handler.HandleAsync(Order(s, elsewhere.LocationId, sku.SkuId));

        Assert.Contains(foreign.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.StockLocationNotFound);
        Assert.DoesNotContain(foreign.ValidationResult.Errors, e => e.ResourceName == InventoryRules.NotFoundResource);
    }

    [Fact]
    public async Task CreatePurchaseOrder_AnInactiveSupplier_IsRefused()
    {
        var s = await NewSetupAsync(supplierActive: false);
        var sku = s.Skus.Add();

        var response = await s.Handler.HandleAsync(Order(s, s.Warehouse.LocationId, sku.SkuId));

        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.SupplierInactive);
    }

    [Fact]
    public async Task CancelPurchaseOrder_WhenAReceiptReferencesIt_IsRefused()
    {
        var s = await NewSetupAsync();
        var sku = s.Skus.Add();
        await s.Handler.HandleAsync(Order(s, s.Warehouse.LocationId, sku.SkuId));
        var order = s.Orders.Saved.Single();
        order.Issue(Today, DateTimeOffset.UtcNow, new InventoryActor(TestUserContext.Create(), Guid.NewGuid()));
        s.Positions.PurchaseOrdersWithReceipts.Add(order.Id);

        var cancel = new CancelPurchaseOrderCommandHandler(NullLogger<CancelPurchaseOrderCommandHandler>.Instance, s.Orders, s.Positions);
        var response = await cancel.HandleAsync(new CancelPurchaseOrderCommand
        { PurchaseOrderId = order.Id, BranchId = Branch, Reason = "Changed my mind.", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.PurchaseOrderHasReceipts);
        Assert.Equal(PurchaseOrderStatus.Issued, order.Status);
    }

    [Fact]
    public async Task IssuePurchaseOrder_OfAnotherBranch_IsNotFound()
    {
        var s = await NewSetupAsync();
        var sku = s.Skus.Add();
        await s.Handler.HandleAsync(Order(s, s.Warehouse.LocationId, sku.SkuId));
        var order = s.Orders.Saved.Single();

        var issue = new IssuePurchaseOrderCommandHandler(
            NullLogger<IssuePurchaseOrderCommandHandler>.Instance, s.Orders, new FakeAggregateRootRepository<SupplierAggregate>(),
            new FakeInventoryClock(Today), TimeProvider.System);
        var response = await issue.HandleAsync(new IssuePurchaseOrderCommand
        { PurchaseOrderId = order.Id, BranchId = Guid.NewGuid(), UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.Equal(InventoryRules.NotFoundResource, Assert.Single(response.ValidationResult!.Errors).ResourceName);
    }
}
