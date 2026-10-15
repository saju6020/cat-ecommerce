using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Application.Tests.TestSupport;

namespace CatEcommerce.Application.Tests.Validators;

/// <summary>HO-IMP-001 §11.2 -- one invalid case per validator family: SHAPE only, asserting the property the failure attaches to. Business rules
/// (expiry, FEFO, stock sufficiency, approval, transitions) are the aggregate's, port's or handler's and are tested there.</summary>
public sealed class InventoryValidatorsTests
{
    private static readonly Guid Id = Guid.NewGuid();

    [Fact]
    public void CreateStockLocation_BadCodeAndUnknownKind_FailOnThoseProperties()
    {
        var result = new CreateStockLocationCommandValidator().Validate(new CreateStockLocationCommand
        {
            LocationId = Id, BranchId = Id, Code = "bad code!", Name = "Warehouse", Kind = "Shed",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateStockLocationCommand.Code));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateStockLocationCommand.Kind));
    }

    [Fact]
    public void CreateStockLocation_AValidCommandPasses_AndKindIsCaseInsensitive()
    {
        var result = new CreateStockLocationCommandValidator().Validate(new CreateStockLocationCommand
        {
            LocationId = Id, BranchId = Id, Code = "WH-2", Name = "Warehouse 2", Kind = "warehouse",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SetProductStorageProfile_UnknownCondition_FailsOnCondition_ButTheTemperatureRangeIsTheAggregates()
    {
        var invalid = new SetProductStorageProfileCommandValidator().Validate(new SetProductStorageProfileCommand
        { ProductId = Id, Condition = "Tepid", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });
        Assert.Contains(invalid.Errors, e => e.PropertyName == nameof(SetProductStorageProfileCommand.Condition));

        var inverted = new SetProductStorageProfileCommandValidator().Validate(new SetProductStorageProfileCommand
        { ProductId = Id, Condition = "Frozen", MinTemperatureC = 8, MaxTemperatureC = 2, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });
        Assert.True(inverted.IsValid);
    }

    [Theory]
    [InlineData("de", "EUR", nameof(CreateSupplierCommand.CountryCode))]
    [InlineData("DE", "eur", nameof(CreateSupplierCommand.CurrencyCode))]
    [InlineData("DE", "EU", nameof(CreateSupplierCommand.CurrencyCode))]
    public void CreateSupplier_CountryAndCurrencyShape_FailOnTheirProperty(string country, string currency, string property)
    {
        var result = new CreateSupplierCommandValidator().Validate(new CreateSupplierCommand
        {
            SupplierId = Id, Code = "SUP-1", Name = "Deutsche Feed", CountryCode = country, CurrencyCode = currency, LeadTimeDays = 5, PaymentTerms = "30 days",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public void CreateSupplier_ABlankPaymentTerms_FailsOnPaymentTerms()
    {
        var result = new CreateSupplierCommandValidator().Validate(new CreateSupplierCommand
        {
            SupplierId = Id, Code = "SUP-1", Name = "Deutsche Feed", CountryCode = "DE", CurrencyCode = "EUR", LeadTimeDays = 5, PaymentTerms = " ",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateSupplierCommand.PaymentTerms));
    }

    private static CreatePurchaseOrderCommand Order(params PurchaseOrderLineInput[] lines) => new()
    {
        PurchaseOrderId = Id, BranchId = Id, SupplierId = Id, DeliverToLocationId = Id, ExpectedDeliveryOn = new DateOnly(2026, 11, 1), Lines = lines,
        UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
    };

    [Fact]
    public void CreatePurchaseOrder_NoLinesTooManyLinesAndBadLineValues_Fail()
    {
        var none = new CreatePurchaseOrderCommandValidator().Validate(Order());
        Assert.Contains(none.Errors, e => e.PropertyName == nameof(CreatePurchaseOrderCommand.Lines));

        var tooMany = new CreatePurchaseOrderCommandValidator().Validate(
            Order(Enumerable.Range(0, 201).Select(_ => new PurchaseOrderLineInput(Guid.NewGuid(), Guid.NewGuid(), 1m, 1m)).ToArray()));
        Assert.Contains(tooMany.Errors, e => e.PropertyName == nameof(CreatePurchaseOrderCommand.Lines));

        var bad = new CreatePurchaseOrderCommandValidator().Validate(Order(
            new PurchaseOrderLineInput(Guid.NewGuid(), Guid.NewGuid(), 0m, 1m),
            new PurchaseOrderLineInput(Guid.NewGuid(), Guid.NewGuid(), 1.2345m, 1m),
            new PurchaseOrderLineInput(Guid.NewGuid(), Guid.NewGuid(), 1m, -1m)));
        Assert.Equal(3, bad.Errors.Count(e => e.PropertyName.StartsWith("Lines[")));
    }

    [Fact]
    public void CreatePurchaseOrder_AValidOrderPasses()
    {
        var result = new CreatePurchaseOrderCommandValidator().Validate(Order(new PurchaseOrderLineInput(Guid.NewGuid(), Guid.NewGuid(), 12.5m, 3.1234m)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CancelAndClosePurchaseOrder_RequireAReason()
    {
        var cancel = new CancelPurchaseOrderCommandValidator().Validate(new CancelPurchaseOrderCommand
        { PurchaseOrderId = Id, BranchId = Id, Reason = "", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });
        var close = new ClosePurchaseOrderCommandValidator().Validate(new ClosePurchaseOrderCommand
        { PurchaseOrderId = Id, BranchId = Id, Reason = new string('x', 501), UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.Contains(cancel.Errors, e => e.PropertyName == nameof(CancelPurchaseOrderCommand.Reason));
        Assert.Contains(close.Errors, e => e.PropertyName == nameof(ClosePurchaseOrderCommand.Reason));
    }
}
