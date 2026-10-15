using CatEcommerce.Application.CommandHandlers.Order;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Order;

/// <summary>HO-ORD-001 OQ-6 (owner decision 7, 2026-10-09) -- the delivery charge is invoiced under a 0 % tax class; a shop without one is a configuration gap answered as a
/// clean business refusal (a 400), never a 5xx.</summary>
public sealed class OrderBillingTests
{
    private static readonly UserContext Customer = new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Customer"] };

    private static OrderAggregate OrderWithDelivery(decimal delivery)
    {
        var line = new OrderLineDto(
            Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Food", "2 kg", null, ProductReturnClass.Food, false, null, 1, 100m, 100m, 0m, 0m, Guid.NewGuid(), 0m, 0m, 100m, []);
        var order = new OrderAggregate(Guid.NewGuid());
        order.Place(new PlaceOrderDto(
            "ORD-2026-000009", OrderChannel.Online, Customer.UserId, Guid.NewGuid(), FulfilmentMethod.Delivery, OrderPaymentMethod.CashOnDelivery, "BDT",
            new FulfilmentDetails("Rina", "01712345678", "House 1", null, "Dhaka", "Dhaka", "1212", null, null, null, null, null, null, null, null, null),
            [line], new OrderTotals(100m, 0m, 0m, delivery, 0m, 100m + delivery), null, null, new string('a', 64), Guid.NewGuid(), DateTimeOffset.UtcNow, Customer, Guid.NewGuid()));
        return order;
    }

    // FU1 D3 (replaces ADeliveryCharge_WithNoZeroRateTaxClass_IsABusinessRefusal...): DeliveryTaxClassNotConfigured is gone. The DELIVERY class is seeded at 0 %;
    // if it is deleted and no other 0 % class exists the failure is a configuration fault (5xx, nothing written), never a customer-facing refusal.
    [Fact]
    public void ADeliveryCharge_WithNoTaxClassAtAll_IsAConfigurationFault_NotABusinessRefusal()
    {
        var order = OrderWithDelivery(60m);

        Assert.Throws<BillingSeamException>(() => OrderBilling.BuildCharges(order, deliveryTaxClassId: null, new DateOnly(2026, 10, 9)));
    }

    [Fact]
    public void ADeliveryCharge_WithAZeroRateTaxClass_BecomesAFeeCharge_AndNoDeliveryMeansNoFeeAndNoNeedForTheClass()
    {
        var taxClass = Guid.NewGuid();

        var withDelivery = OrderBilling.BuildCharges(OrderWithDelivery(60m), taxClass, new DateOnly(2026, 10, 9));
        Assert.Equal(2, withDelivery.Count);
        Assert.Contains(withDelivery, c => c.ItemKind == ItemKind.Fee && c.TaxClassId == taxClass && c.UnitPrice.Amount == 60m);

        var without = OrderBilling.BuildCharges(OrderWithDelivery(0m), null, new DateOnly(2026, 10, 9));
        Assert.Single(without);
    }
}
