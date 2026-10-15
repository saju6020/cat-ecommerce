using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Aggregates.Inventory;

/// <summary>HO-IMP-001 §15 phase B -- U-B1 ... U-B6: suppliers, purchase-order transitions and the derived receipt progress.</summary>
public sealed class ProcurementTests
{
    private static InventoryActor Actor() => new(TestUserContext.Create(), Guid.NewGuid());

    private static readonly DateOnly Today = new(2026, 10, 7);

    // ── suppliers ───────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static SupplierDto Supplier(string country = "DE", string currency = "EUR", int leadTime = 21, string? email = null) =>
        new("DE-FEED", "Deutsche Feed GmbH", country, "Anna", "+49 30 1234", email, "Berlin", currency, leadTime, "30% advance, 70% against B/L", 30, null, Actor());

    [Fact]
    public void U_B1_Supplier_RefusesAnInvalidCountry()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => new SupplierAggregate(Guid.NewGuid()).Create(Supplier(country: "XX")));
        Assert.Equal(BusinessViolationCodes.InvalidCountryCode, ex.ErrorCode);
    }

    [Fact]
    public void U_B1_Supplier_RefusesALowerCaseCurrency()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => new SupplierAggregate(Guid.NewGuid()).Create(Supplier(currency: "usd")));
        Assert.Equal(BusinessViolationCodes.InvalidCurrencyCode, ex.ErrorCode);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(-1)]
    public void U_B1_Supplier_RefusesALeadTimeOutside0To365(int leadTime)
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => new SupplierAggregate(Guid.NewGuid()).Create(Supplier(leadTime: leadTime)));
        Assert.Equal(BusinessViolationCodes.InvalidSupplier, ex.ErrorCode);
        Assert.Equal("LeadTimeDays", ex.PropertyName);
    }

    [Fact]
    public void U_B1_Supplier_RecordsAllSixFieldsAndNormalisesTheCodeAndEmail()
    {
        var supplier = new SupplierAggregate(Guid.NewGuid());

        supplier.Create(Supplier(email: "Anna@Example.COM"));

        Assert.Equal("DE-FEED", supplier.Code);
        Assert.Equal("Deutsche Feed GmbH", supplier.Name);
        Assert.Equal("DE", supplier.CountryCode);
        Assert.Equal("anna@example.com", supplier.Contact.Email);
        Assert.Equal("EUR", supplier.CurrencyCode);
        Assert.Equal(21, supplier.LeadTimeDays);
        Assert.Equal("30% advance, 70% against B/L", supplier.PaymentTerms);
        Assert.True(supplier.IsActive);
        Assert.Single(supplier.Events);
    }

    [Fact]
    public void U_B1_Supplier_DeactivateAndReactivateAreIdempotentAndNeverDelete()
    {
        var supplier = new SupplierAggregate(Guid.NewGuid());
        supplier.Create(Supplier());

        supplier.Deactivate(Actor());
        supplier.Deactivate(Actor());
        Assert.False(supplier.IsActive);

        supplier.Reactivate(Actor());
        Assert.True(supplier.IsActive);
        Assert.DoesNotContain(typeof(SupplierAggregate).GetMethods().Select(m => m.Name), n => n.StartsWith("Delete") || n.StartsWith("Remove"));
    }

    // ── purchase orders ─────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly SkuSnapshot Snapshot = new("SKU-1", "Kitten food 2 kg", "each", StockClass.Food);

    private static PurchaseOrderLineDto Line(Guid? skuId = null, decimal quantity = 100m, decimal cost = 12.3456m, bool fractional = false) =>
        new(Guid.NewGuid(), skuId ?? Guid.NewGuid(), Snapshot, fractional, quantity, cost);

    private static PurchaseOrderDraftDto Draft(IReadOnlyList<PurchaseOrderLineDto> lines, DateOnly? expected = null, string currency = "USD") =>
        new("PO-2026-000001", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), currency, expected ?? Today.AddDays(14), null, lines, Actor());

    private static PurchaseOrderAggregate Order(PurchaseOrderStatus status)
    {
        var order = new PurchaseOrderAggregate(Guid.NewGuid());
        order.CreateDraft(Draft([Line()]));
        if (status == PurchaseOrderStatus.Issued || status == PurchaseOrderStatus.Closed) order.Issue(Today, DateTimeOffset.UtcNow, Actor());
        if (status == PurchaseOrderStatus.Closed) order.Close("Supplier short-shipped.", Actor());
        if (status == PurchaseOrderStatus.Cancelled) order.Cancel("Ordered by mistake.", hasReceipts: false, Actor());
        return order;
    }

    public static IEnumerable<object[]> Transitions()
    {
        // (status, action, allowed) -- §8.5's arrows and nothing else.
        foreach (var status in new[] { PurchaseOrderStatus.Draft, PurchaseOrderStatus.Issued, PurchaseOrderStatus.Closed, PurchaseOrderStatus.Cancelled })
        {
            yield return [status, "replace", status == PurchaseOrderStatus.Draft];
            yield return [status, "issue", status == PurchaseOrderStatus.Draft];
            yield return [status, "cancel", status is PurchaseOrderStatus s && (s == PurchaseOrderStatus.Draft || s == PurchaseOrderStatus.Issued)];
            yield return [status, "close", status == PurchaseOrderStatus.Issued];
        }
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public void U_B2_PurchaseOrderTransitions_AreExactlyThoseOfSection8_5(PurchaseOrderStatus status, string action, bool allowed)
    {
        var order = Order(status);

        Action act = action switch
        {
            "replace" => () => order.ReplaceDraft(Draft([Line()])),
            "issue" => () => order.Issue(Today, DateTimeOffset.UtcNow, Actor()),
            "cancel" => () => order.Cancel("Because.", hasReceipts: false, Actor()),
            _ => () => order.Close("Short-shipped.", Actor()),
        };

        if (allowed) act();
        else Assert.Equal(BusinessViolationCodes.InvalidPurchaseOrderTransition, Assert.Throws<DomainRuleViolationException>(act).ErrorCode);
    }

    [Fact]
    public void U_B3_AnIssuedOrderNeedsAtLeastOneLine()
    {
        var empty = new PurchaseOrderAggregate(Guid.NewGuid());

        Assert.Equal(BusinessViolationCodes.PurchaseOrderLinesRequired,
            Assert.Throws<DomainRuleViolationException>(() => empty.Issue(Today, DateTimeOffset.UtcNow, Actor())).ErrorCode);
        Assert.Equal(BusinessViolationCodes.PurchaseOrderLinesRequired,
            Assert.Throws<DomainRuleViolationException>(() => new PurchaseOrderAggregate(Guid.NewGuid()).CreateDraft(Draft([]))).ErrorCode);
    }

    [Fact]
    public void U_B3_ExpectedDeliveryYesterdayCannotBeIssued_AndTodayCan()
    {
        var late = new PurchaseOrderAggregate(Guid.NewGuid());
        late.CreateDraft(Draft([Line()], expected: Today.AddDays(-1)));
        Assert.Equal(BusinessViolationCodes.ExpectedDeliveryInPast,
            Assert.Throws<DomainRuleViolationException>(() => late.Issue(Today, DateTimeOffset.UtcNow, Actor())).ErrorCode);

        var onTime = new PurchaseOrderAggregate(Guid.NewGuid());
        onTime.CreateDraft(Draft([Line()], expected: Today));
        onTime.Issue(Today, DateTimeOffset.UtcNow, Actor());
        Assert.Equal(PurchaseOrderStatus.Issued, onTime.Status);
    }

    [Fact]
    public void U_B4_TwoLinesForOneSku_AreRefused()
    {
        var sku = Guid.NewGuid();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            new PurchaseOrderAggregate(Guid.NewGuid()).CreateDraft(Draft([Line(sku), Line(sku, quantity: 5m)])));

        Assert.Equal(BusinessViolationCodes.DuplicatePurchaseOrderSku, ex.ErrorCode);
        Assert.Equal("Lines[1].SkuId", ex.PropertyName);
    }

    [Fact]
    public void U_B4_AFractionalQuantityOnAWholeOnlySkuIsRefused_AndACostKeepsFourDecimals()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            new PurchaseOrderAggregate(Guid.NewGuid()).CreateDraft(Draft([Line(quantity: 1.5m)])));
        Assert.Equal(BusinessViolationCodes.FractionalQuantityNotAllowed, ex.ErrorCode);

        var order = new PurchaseOrderAggregate(Guid.NewGuid());
        order.CreateDraft(Draft([Line(cost: 12.34567m)]));
        Assert.Equal(12.3457m, order.Lines[0].UnitCostAmount);
    }

    [Fact]
    public void U_B5_PurchaseOrderProgress_Ordered100Accepted60Rejected10_Outstanding40_PartiallyReceived()
    {
        var line = PurchaseOrderProgress.ForLine(ordered: 100m, accepted: 60m, rejected: 10m);

        Assert.Equal(40m, line.Outstanding);
        Assert.Equal(PurchaseOrderReceiptState.PartiallyReceived, PurchaseOrderProgress.StateOf([line]));
    }

    [Fact]
    public void U_B5_ARejectedUnitIsStillOwed_AndNothingReceivedIsNotReceived_AndAllAcceptedIsFullyReceived()
    {
        Assert.Equal(PurchaseOrderReceiptState.NotReceived, PurchaseOrderProgress.StateOf([PurchaseOrderProgress.ForLine(10m, 0m, 0m)]));
        Assert.Equal(PurchaseOrderReceiptState.PartiallyReceived, PurchaseOrderProgress.StateOf([PurchaseOrderProgress.ForLine(10m, 0m, 10m)]));
        Assert.Equal(10m, PurchaseOrderProgress.ForLine(10m, 0m, 10m).Outstanding);
        Assert.Equal(PurchaseOrderReceiptState.FullyReceived,
            PurchaseOrderProgress.StateOf([PurchaseOrderProgress.ForLine(10m, 10m, 0m), PurchaseOrderProgress.ForLine(4m, 4m, 2m)]));
        Assert.Equal(0m, PurchaseOrderProgress.ForLine(10m, 12m, 0m).Outstanding);
    }

    [Fact]
    public void U_B6_CancelWithReceiptsIsRefused_AndCloseNeedsAReason()
    {
        var issued = Order(PurchaseOrderStatus.Issued);

        Assert.Equal(BusinessViolationCodes.PurchaseOrderHasReceipts,
            Assert.Throws<DomainRuleViolationException>(() => issued.Cancel("Changed my mind.", hasReceipts: true, Actor())).ErrorCode);
        Assert.Equal(BusinessViolationCodes.CloseReasonRequired,
            Assert.Throws<DomainRuleViolationException>(() => issued.Close("  ", Actor())).ErrorCode);
        Assert.Equal(PurchaseOrderStatus.Issued, issued.Status);
    }
}
