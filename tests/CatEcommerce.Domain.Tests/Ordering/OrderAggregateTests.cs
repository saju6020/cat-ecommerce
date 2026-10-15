using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Order;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Tests.Ordering;

/// <summary>HO-ORD-001 §15 groups A, D, E, F, G, H -- the order aggregate's own rules (US-ORD-08, 09, 12, 20; ADR-197).</summary>
public sealed class OrderAggregateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly UserContext Customer = new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Customer"] };
    private static readonly UserContext Staff = new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Reception"] };
    private static readonly UserContext OtherStaff = new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Accountant"] };
    private static readonly Guid Corr = Guid.NewGuid();

    // One order of two lines: 2 x 850.00 at 15 % and 1 x 120.00 at 0 %, a 182.00 order discount, delivery 60.00 -> total 1927.50 (U-D1).
    private static PlaceOrderDto Dto(
        OrderPaymentMethod? method = null, FulfilmentMethod? fulfilment = null, OrderChannel? channel = null, Action<List<OrderLineDto>>? lines = null,
        OrderTotals? totals = null, UserContext? user = null)
    {
        var lineList = new List<OrderLineDto>
        {
            new(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Food", "Food", null, ProductReturnClass.Food, false, null, 2, 850m, 1700m, 0m, 170m, Guid.NewGuid(), 15m, 229.50m, 1759.50m, []),
            new(Guid.NewGuid(), 2, Guid.NewGuid(), Guid.NewGuid(), "Toy", "Toy", null, ProductReturnClass.Other, false, null, 1, 120m, 120m, 0m, 12m, Guid.NewGuid(), 0m, 0m, 108m, []),
        };
        lines?.Invoke(lineList);

        var isCounter = (channel ?? OrderChannel.Online) == OrderChannel.Counter;
        var payment = method ?? (isCounter ? OrderPaymentMethod.InStore : OrderPaymentMethod.CashOnDelivery);
        var fulfil = fulfilment ?? (isCounter ? FulfilmentMethod.Counter : payment == OrderPaymentMethod.PayOnCollection ? FulfilmentMethod.Pickup : FulfilmentMethod.Delivery);

        return new PlaceOrderDto(
            "ORD-2026-000001", channel ?? OrderChannel.Online, isCounter ? null : Customer.UserId, Guid.NewGuid(), fulfil, payment, "BDT",
            new FulfilmentDetails("Rina Akter", "01712345678", "House 1", null, "Dhaka", "Dhaka", "1212", null, null, null, null, null, null, null, null, null),
            lineList, totals ?? new OrderTotals(1820m, 0m, 182m, 60m, 229.50m, 1927.50m), null, null, new string('f', 64), Guid.NewGuid(), T0, user ?? Customer, Corr);
    }

    private static OrderAggregate Placed(OrderPaymentMethod? method = null, FulfilmentMethod? fulfilment = null, OrderChannel? channel = null, Action<List<OrderLineDto>>? lines = null, OrderTotals? totals = null)
    {
        var order = new OrderAggregate(Guid.NewGuid());
        order.Place(Dto(method, fulfilment, channel, lines, totals));
        return order;
    }

    private static OrderAggregate Allocated(OrderAggregate order)
    {
        order.RecordAllocations(order.Lines.Select(l => new AllocationDto(l.Id, l.SkuId, Guid.NewGuid(), "LOT-1", Guid.NewGuid(), l.Quantity, null)).ToList());
        return order;
    }

    private static OrderAggregate Packing(OrderPaymentMethod method = default!, FulfilmentMethod? fulfilment = null)
    {
        var order = Allocated(Placed(method ?? OrderPaymentMethod.CashOnDelivery, fulfilment));
        order.StartPacking(Staff, T0.AddHours(1), Corr);
        return order;
    }

    private static Shipment Ship() => new(Guid.NewGuid(), "Sundarban", null, null, T0.AddHours(2), Staff.UserId, null, null);

    private static string CodeOf(Action action) => Assert.Throws<DomainRuleViolationException>(action).ErrorCode;

    // ── placement ───────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void ACodOrder_IsAcceptedAtPlacement_WithOneHistoryRowAndBothEvents()
    {
        var order = Placed(OrderPaymentMethod.CashOnDelivery);

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(T0, order.ConfirmedOn);
        var row = Assert.Single(order.StatusHistory);
        Assert.Null(row.From);
        Assert.Equal(OrderStatus.Confirmed, row.To);
        Assert.Equal(Customer.UserId, row.ChangedBy);
        Assert.Equal(T0, row.ChangedOn);
        Assert.Contains(order.Events, e => e is OrderPlacedEvent);
        Assert.Contains(order.Events, e => e is OrderConfirmedEvent);
        Assert.Equal(1927.50m, order.Totals.GrandTotal);
        Assert.Equal(2, order.Lines.Count);
    }

    [Fact]
    public void ABankTransferOrder_StartsPendingPayment_AndAwaitsTheTransfer()
    {
        var order = Placed(OrderPaymentMethod.BankTransfer);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.DoesNotContain(order.Events, e => e is OrderConfirmedEvent);

        order.AwaitTransfer(T0.AddHours(48));
        Assert.Equal(T0.AddHours(48), order.PaymentDueBy);

        Assert.Throws<InvalidOperationException>(() => Placed(OrderPaymentMethod.CashOnDelivery).AwaitTransfer(T0));
    }

    [Fact]
    public void U_D4_PlacingWithTotalsThatDoNotReSum_IsAProgrammingError()
    {
        Assert.Throws<InvalidOperationException>(() => new OrderAggregate(Guid.NewGuid()).Place(Dto(totals: new OrderTotals(1820m, 0m, 182m, 60m, 229.50m, 1927.49m))));
        Assert.Throws<InvalidOperationException>(() => new OrderAggregate(Guid.NewGuid()).Place(Dto(totals: new OrderTotals(1820.01m, 0m, 182m, 60m, 229.50m, 1927.51m))));
        Assert.Throws<InvalidOperationException>(() => new OrderAggregate(Guid.NewGuid()).Place(Dto(lines: l => l[0] = l[0] with { LineTotal = 1759.51m })));
        Assert.Throws<InvalidOperationException>(() => new OrderAggregate(Guid.NewGuid()).Place(Dto(lines: l => l[0] = l[0] with { LineSubtotal = 1699.99m })));
        Assert.Throws<InvalidOperationException>(() => new OrderAggregate(Guid.NewGuid()).Place(Dto(lines: l => l.Clear())));
    }

    // FU1 D3: a configured delivery tax (15 % of 60.00 = 9.00) is part of the tax total and the grand total, and is declared separately from the lines' own tax.
    [Fact]
    public void U_D4_ADeliveryTax_IsPartOfTheTaxTotal_OnlyWhenTheOrderDeclaresIt()
    {
        var totals = new OrderTotals(1820m, 0m, 182m, 60m, 238.50m, 1936.50m);
        var declared = Dto(totals: totals) with { DeliveryTaxAmount = 9.00m };

        var order = new OrderAggregate(Guid.NewGuid());
        order.Place(declared);                                                                       // re-sums: 229.50 + 9.00 = 238.50
        Assert.Equal(1936.50m, order.Totals.GrandTotal);

        Assert.Throws<InvalidOperationException>(() => new OrderAggregate(Guid.NewGuid()).Place(Dto(totals: totals)));                                  // undeclared
        Assert.Throws<InvalidOperationException>(() => new OrderAggregate(Guid.NewGuid()).Place(declared with { DeliveryTaxAmount = 8.99m }));          // mis-declared
    }

    // FU1 ORD-55/56: the delivery tax is stored on the order; it is 0 without a delivery charge, never negative and never above the charge.
    [Fact]
    public void U_D4_TheDeliveryTax_IsStoredOnTheOrder_AndIsBoundedByTheDeliveryCharge()
    {
        var order = new OrderAggregate(Guid.NewGuid());
        order.Place(Dto(totals: new OrderTotals(1820m, 0m, 182m, 60m, 238.50m, 1936.50m)) with { DeliveryTaxAmount = 9.00m });
        Assert.Equal(9.00m, order.DeliveryTaxAmount);

        // no delivery charge, but a delivery tax declared (the line tax is balanced so only the bound can refuse it)
        Assert.Throws<InvalidOperationException>(() => new OrderAggregate(Guid.NewGuid()).Place(
            Dto(totals: new OrderTotals(1820m, 0m, 182m, 0m, 238.50m, 1876.50m)) with { DeliveryTaxAmount = 9.00m }));

        // above the charge (and negative), with totals that would otherwise re-sum
        Assert.Throws<InvalidOperationException>(() => new OrderAggregate(Guid.NewGuid()).Place(
            Dto(totals: new OrderTotals(1820m, 0m, 182m, 60m, 229.50m + 61m, 1936.50m - 9m + 61m)) with { DeliveryTaxAmount = 61m }));
        Assert.Throws<InvalidOperationException>(() => new OrderAggregate(Guid.NewGuid()).Place(
            Dto(totals: new OrderTotals(1820m, 0m, 182m, 60m, 220.50m, 1918.50m)) with { DeliveryTaxAmount = -9.00m }));
    }

    [Theory]
    [InlineData(nameof(OrderPaymentMethod.CashOnDelivery), nameof(FulfilmentMethod.Pickup))]
    [InlineData(nameof(OrderPaymentMethod.PayOnCollection), nameof(FulfilmentMethod.Delivery))]
    [InlineData(nameof(OrderPaymentMethod.BankTransfer), nameof(FulfilmentMethod.Counter))]
    [InlineData(nameof(OrderPaymentMethod.InStore), nameof(FulfilmentMethod.Delivery))]
    public void AMethodThatDoesNotGoWithTheFulfilment_IsRefused(string method, string fulfilment) =>
        Assert.Throws<InvalidOperationException>(() => new OrderAggregate(Guid.NewGuid()).Place(Dto(
            Enumeration.FromDisplayName<OrderPaymentMethod>(method), Enumeration.FromDisplayName<FulfilmentMethod>(fulfilment))));

    [Fact]
    public void AnOnlineOrderWithNoCustomer_IsRefused_ButACounterSaleMayBeAnonymous()
    {
        var anonymousOnline = Dto() with { CustomerId = null };
        Assert.Throws<InvalidOperationException>(() => new OrderAggregate(Guid.NewGuid()).Place(anonymousOnline));

        var counter = Placed(channel: OrderChannel.Counter);
        Assert.Null(counter.CustomerId);
        Assert.Equal(OrderStatus.PendingPayment, counter.Status);
    }

    [Fact]
    public void AnOrderHoldsAtMost50Lines()
    {
        var many = Enumerable.Range(1, 51).Select(n => new OrderLineDto(
            Guid.NewGuid(), n, Guid.NewGuid(), Guid.NewGuid(), "P", "P", null, ProductReturnClass.Other, false, null, 1, 1m, 1m, 0m, 0m, Guid.NewGuid(), 0m, 0m, 1m, [])).ToList();
        var dto = Dto() with { Lines = many, Totals = new OrderTotals(51m, 0m, 0m, 0m, 0m, 51m) };

        Assert.Throws<InvalidOperationException>(() => new OrderAggregate(Guid.NewGuid()).Place(dto));
    }

    // ── U-A5 / U-A6 ─────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_A6_EveryTransition_AppendsExactlyOneAttributedTimestampedRow()
    {
        var order = Packing(OrderPaymentMethod.CashOnDelivery);
        Assert.Equal(2, order.StatusHistory.Count);
        Assert.Equal((OrderStatus.Confirmed, OrderStatus.Packing, Staff.UserId, T0.AddHours(1)), (order.StatusHistory[1].From!, order.StatusHistory[1].To, order.StatusHistory[1].ChangedBy, order.StatusHistory[1].ChangedOn));

        order.Dispatch(Ship(), T0.AddHours(2), Staff, Corr);
        Assert.Equal(3, order.StatusHistory.Count);
        Assert.Equal(OrderStatus.Dispatched, order.StatusHistory[2].To);

        var cod = new CodCollection(1927.50m, CollectorKind.Courier, "Rider", null, T0.AddHours(5), Staff.UserId);
        order.RecordDelivery(T0.AddHours(5), cod, null, T0.AddHours(5), Staff, Corr);
        Assert.Equal(4, order.StatusHistory.Count);
        Assert.Equal(order.StatusHistory.Select(h => h.To), order.StatusHistory.Select(h => h.To).ToList());
        Assert.All(order.StatusHistory, h => Assert.NotEqual(Guid.Empty, h.ChangedBy));
    }

    [Fact]
    public void U_A5_AnInvalidMove_IsInvalidOrderTransition_NamingTheState_AndAddsNoRow()
    {
        var order = Packing(OrderPaymentMethod.CashOnDelivery);
        order.Dispatch(Ship(), T0.AddHours(2), Staff, Corr);
        order.RecordDelivery(T0.AddHours(5), new CodCollection(1927.50m, CollectorKind.Staff, "Rima", Staff.UserId, T0.AddHours(5), Staff.UserId), null, T0.AddHours(5), Staff, Corr);
        var rows = order.StatusHistory.Count;

        var ex = Assert.Throws<DomainRuleViolationException>(() => order.StartPacking(Staff, T0.AddHours(6), Corr));
        Assert.Equal(BusinessViolationCodes.InvalidOrderTransition, ex.ErrorCode);
        Assert.Equal("A delivered order cannot be packed.", ex.Message);
        Assert.Equal(rows, order.StatusHistory.Count);

        Assert.Equal(BusinessViolationCodes.InvalidOrderTransition, CodeOf(() => order.Dispatch(Ship(), T0, Staff, Corr)));
        Assert.Equal(BusinessViolationCodes.InvalidOrderTransition, CodeOf(() => order.RecordDelivery(T0, null, null, T0, Staff, Corr)));
    }

    // ── U-E1 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_E1_Expire_OneTickBeforeTheDueTime_IsNotExpirable_AtItCancelsWithTheReason()
    {
        var order = Placed(OrderPaymentMethod.BankTransfer);
        order.AwaitTransfer(T0.AddHours(48));

        Assert.Equal(BusinessViolationCodes.OrderNotExpirable, CodeOf(() => order.Expire(T0.AddHours(48).AddTicks(-1), Staff, Corr)));
        Assert.Equal(OrderStatus.PendingPayment, order.Status);

        order.Expire(T0.AddHours(48), Staff, Corr);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(OrderCancellationReason.PaymentWindowExpired, order.CancellationReason);
        Assert.Equal("Payment window expired", order.StatusHistory[^1].Note);
        var cancelled = Assert.Single(order.Events.OfType<OrderCancelledEvent>());
        Assert.False(cancelled.WasPaid);
        Assert.Equal(OrderCancellationReason.PaymentWindowExpired.Value, cancelled.Reason);
    }

    [Fact]
    public void U_E1_AHeldPayment_IsNotExpired_AndNeitherIsAnOrderThatIsNotBankTransfer()
    {
        var order = Placed(OrderPaymentMethod.BankTransfer);
        order.AwaitTransfer(T0.AddHours(48));
        order.HoldPaymentForApproval(new HeldPaymentConfirmation(1927.50m, "TRX-1", T0, Staff.UserId, T0), Staff, Corr);

        Assert.Equal(BusinessViolationCodes.OrderNotExpirable, CodeOf(() => order.Expire(T0.AddDays(10), Staff, Corr)));
        Assert.Equal(BusinessViolationCodes.OrderNotExpirable, CodeOf(() => Placed(OrderPaymentMethod.CashOnDelivery).Expire(T0.AddDays(10), Staff, Corr)));
    }

    // ── U-E2 / U-E3 ─────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_E2_Cancel_ByTheCustomerFromConfirmed_IsAllowed()
    {
        var order = Placed(OrderPaymentMethod.CashOnDelivery);
        order.Cancel(Customer, true, OrderCancellationReason.CustomerRequest, null, T0.AddMinutes(5), Corr);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.True(order.CancelledByCustomer);
        Assert.Equal(OrderCancellationReason.CustomerRequest, order.CancellationReason);
        Assert.Equal(T0.AddMinutes(5), order.CancelledOn);
    }

    [Fact]
    public void U_E2_Cancel_ByTheCustomerFromPacking_IsCancellationNotAvailable_ByStaffIsAllowed()
    {
        var order = Packing();

        var ex = Assert.Throws<DomainRuleViolationException>(() => order.Cancel(Customer, true, OrderCancellationReason.CustomerRequest, null, T0.AddHours(2), Corr));
        Assert.Equal(BusinessViolationCodes.CancellationNotAvailable, ex.ErrorCode);
        Assert.Equal("Packing has started, so this order can no longer be cancelled online. Please contact the shop.", ex.Message);
        Assert.Equal(OrderStatus.Packing, order.Status);

        order.Cancel(Staff, false, OrderCancellationReason.StaffDecision, "Out of stock after a count", T0.AddHours(2), Corr);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.False(order.CancelledByCustomer);
        Assert.Equal("Out of stock after a count", order.CancellationNote);
    }

    [Fact]
    public void U_E2_ACustomer_CannotCancelADispatchedOrDeliveredOne_AndStaffNeedAReason()
    {
        var dispatched = Packing();
        dispatched.Dispatch(Ship(), T0.AddHours(2), Staff, Corr);
        Assert.Equal(BusinessViolationCodes.CancellationNotAvailable, CodeOf(() => dispatched.Cancel(Customer, true, OrderCancellationReason.CustomerRequest, null, T0, Corr)));
        Assert.Equal(BusinessViolationCodes.InvalidOrderTransition, CodeOf(() => dispatched.Cancel(Staff, false, OrderCancellationReason.StaffDecision, "x", T0, Corr)));

        var confirmed = Placed();
        Assert.Equal(BusinessViolationCodes.CancellationReasonRequired, CodeOf(() => confirmed.Cancel(Staff, false, OrderCancellationReason.StaffDecision, "  ", T0, Corr)));
        Assert.Equal(BusinessViolationCodes.CancellationReasonRequired, CodeOf(() => confirmed.Cancel(Staff, false, OrderCancellationReason.StaffDecision, new string('x', 501), T0, Corr)));
    }

    [Fact]
    public void U_E2_ACancelledOrder_CannotBeCancelledAgain()
    {
        var order = Placed();
        order.Cancel(Customer, true, OrderCancellationReason.CustomerRequest, null, T0, Corr);

        Assert.Equal(BusinessViolationCodes.InvalidOrderTransition, CodeOf(() => order.Cancel(Customer, true, OrderCancellationReason.CustomerRequest, null, T0, Corr)));
    }

    [Fact]
    public void U_E3_CancellingAPaidOrder_RaisesARefundObligation_AndTheEventSaysWasPaid()
    {
        var order = Placed(OrderPaymentMethod.BankTransfer);
        order.AwaitTransfer(T0.AddHours(48));
        order.ConfirmPayment(Guid.NewGuid(), "BankTransfer", 1927.50m, T0.AddHours(1), Staff, Corr);
        Assert.Equal(1927.50m, order.AmountPaid);

        order.Cancel(Customer, true, OrderCancellationReason.CustomerRequest, null, T0.AddHours(2), Corr);

        Assert.Equal(1927.50m, order.RefundDue);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.True(order.Events.OfType<OrderCancelledEvent>().Single().WasPaid);
    }

    [Fact]
    public void ADiscountUse_IsReleasedOnceOnly_AndOnlyWhenTheOrderUsedACode()
    {
        var withCode = new OrderAggregate(Guid.NewGuid());
        withCode.Place(Dto() with { DiscountCodeId = Guid.NewGuid(), DiscountCodeText = "SAVE10" });
        Assert.True(withCode.ReleaseDiscount());
        Assert.False(withCode.ReleaseDiscount());

        Assert.False(Placed().ReleaseDiscount());
    }

    // ── U-E4 / U-E5 and the second approval ─────────────────────────────────────────────────────────
    [Fact]
    public void U_E5_ATransferOf1927_49_AgainstA1927_50Order_IsTransferAmountMismatch()
    {
        var order = Placed(OrderPaymentMethod.BankTransfer);
        order.AwaitTransfer(T0.AddHours(48));

        Assert.Equal(BusinessViolationCodes.TransferAmountMismatch, CodeOf(() => order.ConfirmPayment(Guid.NewGuid(), "BankTransfer", 1927.49m, T0, Staff, Corr)));
        Assert.Equal(BusinessViolationCodes.TransferAmountMismatch, CodeOf(() => order.ConfirmPayment(Guid.NewGuid(), "BankTransfer", 1927.51m, T0, Staff, Corr)));
        Assert.Equal(BusinessViolationCodes.TransferAmountMismatch, CodeOf(() => order.HoldPaymentForApproval(new HeldPaymentConfirmation(1927.49m, "R", T0, Staff.UserId, T0), Staff, Corr)));
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(0m, order.AmountPaid);
    }

    [Fact]
    public void U_E4_TheRecorder_CannotApproveTheirOwnHeldPayment_ADifferentPersonCan()
    {
        var order = Placed(OrderPaymentMethod.BankTransfer);
        order.AwaitTransfer(T0.AddHours(48));
        Assert.Equal(BusinessViolationCodes.NoPaymentAwaitingApproval, CodeOf(() => order.RequireHeldPaymentForApprover(OtherStaff.UserId)));

        order.HoldPaymentForApproval(new HeldPaymentConfirmation(1927.50m, "TRX-9", T0, Staff.UserId, T0), Staff, Corr);
        Assert.Equal(BusinessViolationCodes.PaymentAlreadyAwaitingApproval,
            CodeOf(() => order.HoldPaymentForApproval(new HeldPaymentConfirmation(1927.50m, "TRX-10", T0, Staff.UserId, T0), Staff, Corr)));

        Assert.Equal(BusinessViolationCodes.SecondApproverMustDiffer, CodeOf(() => order.RequireHeldPaymentForApprover(Staff.UserId)));
        var held = order.RequireHeldPaymentForApprover(OtherStaff.UserId);
        Assert.Equal("TRX-9", held.Reference);

        order.ConfirmPayment(Guid.NewGuid(), "BankTransfer", held.Amount, T0.AddMinutes(10), OtherStaff, Corr);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Null(order.HeldPayment);
        Assert.Contains(order.Events, e => e is OrderPaymentHeldForApprovalEvent);
        Assert.Single(order.Payments);
    }

    [Fact]
    public void ARejectedHeldPayment_IsCleared_AndTheOrderStaysPending()
    {
        var order = Placed(OrderPaymentMethod.BankTransfer);
        order.AwaitTransfer(T0.AddHours(48));
        Assert.Equal(BusinessViolationCodes.NoPaymentAwaitingApproval, CodeOf(() => order.RejectHeldPayment(OtherStaff, Corr)));

        order.HoldPaymentForApproval(new HeldPaymentConfirmation(1927.50m, "TRX-9", T0, Staff.UserId, T0), Staff, Corr);
        order.RejectHeldPayment(OtherStaff, Corr);

        Assert.Null(order.HeldPayment);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Contains(order.Events, e => e is OrderHeldPaymentRejectedEvent);
    }

    [Fact]
    public void ATransferReference_CanBeSubmittedAndOverwrittenWhilePending_NotAfter()
    {
        var order = Placed(OrderPaymentMethod.BankTransfer);
        order.AwaitTransfer(T0.AddHours(48));

        order.SubmitTransferReference("  TRX-A ", T0.AddMinutes(1), Customer, Corr);
        order.SubmitTransferReference("TRX-B", T0.AddMinutes(2), Customer, Corr);
        Assert.Equal("TRX-B", order.CustomerTransferReference);
        Assert.Equal(T0.AddMinutes(2), order.TransferReportedOn);

        Assert.Equal(BusinessViolationCodes.OrderNotAwaitingPayment, CodeOf(() => order.SubmitTransferReference(" ", T0, Customer, Corr)));
        order.ConfirmPayment(null, "BankTransfer", 1927.50m, T0, Staff, Corr);
        Assert.Equal(BusinessViolationCodes.OrderNotAwaitingPayment, CodeOf(() => order.SubmitTransferReference("TRX-C", T0, Customer, Corr)));
        Assert.Equal(BusinessViolationCodes.OrderNotAwaitingPayment, CodeOf(() => Placed(OrderPaymentMethod.CashOnDelivery).SubmitTransferReference("X", T0, Customer, Corr)));
    }

    // ── allocation and U-F1 ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void RecordAllocations_RequiresTheExactQuantityPerSku_AndAReplacementReplaces()
    {
        var order = Placed();
        var food = order.Lines[0];

        var short1 = new[] { new AllocationDto(food.Id, food.SkuId, Guid.NewGuid(), "L1", Guid.NewGuid(), 1m, null) };
        Assert.Throws<InvalidOperationException>(() => order.RecordAllocations(short1));
        Assert.Throws<InvalidOperationException>(() => order.RecordAllocations([new AllocationDto(food.Id, food.SkuId, Guid.NewGuid(), "L1", Guid.NewGuid(), 3m, null)]));
        Assert.Throws<InvalidOperationException>(() => order.RecordAllocations([new AllocationDto(food.Id, Guid.NewGuid(), Guid.NewGuid(), "L1", Guid.NewGuid(), 2m, null)]));
        Assert.Throws<InvalidOperationException>(() => order.RecordAllocations([new AllocationDto(Guid.NewGuid(), food.SkuId, Guid.NewGuid(), "L1", Guid.NewGuid(), 2m, null)]));

        var split = new[]
        {
            new AllocationDto(food.Id, food.SkuId, Guid.NewGuid(), "L1", Guid.NewGuid(), 1m, new DateOnly(2027, 1, 1)),
            new AllocationDto(food.Id, food.SkuId, Guid.NewGuid(), "L2", Guid.NewGuid(), 1m, new DateOnly(2027, 2, 1)),
        };
        order.RecordAllocations(split);
        Assert.Equal(2, food.Allocations.Count);
        Assert.False(order.IsFullyAllocated); // the toy line has none yet

        var replacement = new AllocationDto(food.Id, food.SkuId, Guid.NewGuid(), "L3", Guid.NewGuid(), 2m, null);
        order.RecordAllocations([replacement]);
        Assert.Equal("L3", Assert.Single(food.Allocations).LotNumber);
    }

    [Fact]
    public void ABundleLine_IsAllocatedPerComponent()
    {
        var componentA = Guid.NewGuid();
        var componentB = Guid.NewGuid();
        var order = Placed(lines: l =>
        {
            l.Clear();
            l.Add(new OrderLineDto(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), "Starter kit", "Kit", null, ProductReturnClass.Other, true, null, 2, 100m, 200m, 0m, 0m,
                Guid.NewGuid(), 0m, 0m, 200m, [new OrderLineComponent(componentA, 1), new OrderLineComponent(componentB, 3)]));
        }, totals: new OrderTotals(200m, 0m, 0m, 60m, 0m, 260m));

        var line = order.Lines[0];
        Assert.Equal(new Dictionary<Guid, int> { [componentA] = 2, [componentB] = 6 }, line.RequiredBySku());

        order.RecordAllocations(
        [
            new AllocationDto(line.Id, componentA, Guid.NewGuid(), "A1", Guid.NewGuid(), 2m, null),
            new AllocationDto(line.Id, componentB, Guid.NewGuid(), "B1", Guid.NewGuid(), 4m, null),
            new AllocationDto(line.Id, componentB, Guid.NewGuid(), "B2", Guid.NewGuid(), 2m, null),
        ]);
        Assert.True(order.IsFullyAllocated);
    }

    [Fact]
    public void U_F1_Dispatch_WithAnUnallocatedLine_IsOrderNotFullyAllocated()
    {
        var order = Placed(OrderPaymentMethod.CashOnDelivery);
        order.StartPacking(Staff, T0, Corr);

        Assert.Equal(BusinessViolationCodes.OrderNotFullyAllocated, CodeOf(() => order.Dispatch(Ship(), T0, Staff, Corr)));
        Assert.Equal(OrderStatus.Packing, order.Status);
        Assert.Null(order.Shipment);

        Allocated(order);
        order.Dispatch(Ship(), T0.AddHours(1), Staff, Corr);
        Assert.Equal(OrderStatus.Dispatched, order.Status);
    }

    // ── U-F2 / U-F3 / U-F4 ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_F2_ACodDelivery_NeedsTheCollection_AndItMustBeTheBalance()
    {
        var order = Packing(OrderPaymentMethod.CashOnDelivery);
        order.Dispatch(Ship(), T0.AddHours(2), Staff, Corr);

        Assert.Equal(BusinessViolationCodes.CodCollectionRequired, CodeOf(() => order.RecordDelivery(T0.AddHours(3), null, null, T0.AddHours(3), Staff, Corr)));
        Assert.Equal(BusinessViolationCodes.CollectionPaymentMismatch,
            CodeOf(() => order.RecordDelivery(T0.AddHours(3), new CodCollection(1000m, CollectorKind.Courier, "Rider", null, T0.AddHours(3), Staff.UserId), null, T0.AddHours(3), Staff, Corr)));
        Assert.Equal(OrderStatus.Dispatched, order.Status);

        order.RecordDelivery(T0.AddHours(3), new CodCollection(1927.50m, CollectorKind.Courier, "Rider", null, T0.AddHours(3), Staff.UserId), null, T0.AddHours(3), Staff, Corr);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal("Rider", order.CodCollection!.CollectorName);
        Assert.True(order.Events.OfType<OrderDeliveredEvent>().Single().HadCodCollection);
        Assert.Equal(Staff.UserId, order.DeliveryRecordedBy);
    }

    [Fact]
    public void U_F2_PayOnCollection_NeedsAPaymentEqualToTheBalance_AndACardNeedsTheSlipReference()
    {
        var order = Allocated(Placed(OrderPaymentMethod.PayOnCollection));
        order.StartPacking(Staff, T0, Corr);
        order.MarkReadyForCollection(new DateOnly(2026, 10, 16), T0.AddHours(1), Staff, Corr);
        Assert.Equal(new DateOnly(2026, 10, 16), order.CollectByDate);
        Assert.Equal(T0.AddHours(1), order.ReadyForCollectionOn);

        Assert.Equal(BusinessViolationCodes.CollectionPaymentMismatch, CodeOf(() => order.RecordDelivery(T0.AddDays(1), null, null, T0.AddDays(1), Staff, Corr)));
        Assert.Equal(BusinessViolationCodes.CollectionPaymentMismatch,
            CodeOf(() => order.RecordDelivery(T0.AddDays(1), null, new CollectionPaymentDto(TenderMethod.Cash, 1900m, null), T0.AddDays(1), Staff, Corr)));
        Assert.Equal(BusinessViolationCodes.TerminalReferenceRequired,
            CodeOf(() => order.RecordDelivery(T0.AddDays(1), null, new CollectionPaymentDto(TenderMethod.InStoreCard, 1927.50m, " "), T0.AddDays(1), Staff, Corr)));

        order.RecordDelivery(T0.AddDays(1), null, new CollectionPaymentDto(TenderMethod.InStoreCard, 1927.50m, "SLIP-123"), T0.AddDays(1), Staff, Corr);
        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void DispatchAndReadyForCollection_AreFor_DeliveryAndPickupRespectively()
    {
        var delivery = Allocated(Placed(OrderPaymentMethod.CashOnDelivery));
        delivery.StartPacking(Staff, T0, Corr);
        Assert.Equal(BusinessViolationCodes.InvalidOrderTransition, CodeOf(() => delivery.MarkReadyForCollection(new DateOnly(2026, 10, 16), T0, Staff, Corr)));

        var pickup = Allocated(Placed(OrderPaymentMethod.PayOnCollection));
        pickup.StartPacking(Staff, T0, Corr);
        Assert.Equal(BusinessViolationCodes.InvalidOrderTransition, CodeOf(() => pickup.Dispatch(Ship(), T0, Staff, Corr)));
    }

    [Fact]
    public void U_F3_UncollectedOnADeliveryOrder_IsInvalid_RefusedOnAPickupOrder_IsInvalid()
    {
        var delivery = Packing(OrderPaymentMethod.CashOnDelivery);
        delivery.Dispatch(Ship(), T0.AddHours(2), Staff, Corr);
        Assert.Equal(BusinessViolationCodes.DeliveryFailureReasonInvalid, CodeOf(() => delivery.RecordDeliveryFailure(DeliveryFailureReason.Uncollected, T0, Staff, Corr)));
        delivery.RecordDeliveryFailure(DeliveryFailureReason.Refused, T0.AddHours(4), Staff, Corr);
        Assert.Equal(OrderStatus.Returned, delivery.Status);
        Assert.Equal(DeliveryFailureReason.Refused, delivery.DeliveryFailureReason);

        var pickup = Allocated(Placed(OrderPaymentMethod.PayOnCollection));
        pickup.StartPacking(Staff, T0, Corr);
        pickup.MarkReadyForCollection(new DateOnly(2026, 10, 16), T0, Staff, Corr);
        Assert.Equal(BusinessViolationCodes.DeliveryFailureReasonInvalid, CodeOf(() => pickup.RecordDeliveryFailure(DeliveryFailureReason.Refused, T0, Staff, Corr)));
        pickup.RecordDeliveryFailure(DeliveryFailureReason.Uncollected, T0.AddDays(8), Staff, Corr);
        Assert.Equal(OrderStatus.Returned, pickup.Status);
    }

    [Fact]
    public void U_F4_ATrackingEvent_OlderThanTheLast_IsIgnored_AndDeliveredIsNeverInferredFromIt()
    {
        var order = Packing();
        order.Dispatch(Ship(), T0.AddHours(2), Staff, Corr);

        Assert.True(order.ApplyTrackingEvent("in_transit", T0.AddHours(3)));
        Assert.False(order.ApplyTrackingEvent("picked_up", T0.AddHours(2)));  // older
        Assert.False(order.ApplyTrackingEvent("picked_up", T0.AddHours(3)));  // same instant
        Assert.Equal("in_transit", order.Shipment!.LastTrackingStatus);

        Assert.True(order.ApplyTrackingEvent("delivered", T0.AddHours(6)));
        Assert.Equal(OrderStatus.Dispatched, order.Status); // COD money must be recorded by a person
        Assert.False(Placed().ApplyTrackingEvent("x", T0)); // an order not dispatched has no shipment
    }

    [Fact]
    public void ATrackingNumber_IsSetOnce_AndTheFirstNumberIsReported()
    {
        var order = Packing();
        order.Dispatch(Ship(), T0.AddHours(2), Staff, Corr);
        Assert.True(order.Events.OfType<OrderDispatchedEvent>().Single().HasTracking == false);

        Assert.True(order.RecordTracking(" TRK-1 ", "https://track.example/TRK-1"));
        Assert.False(order.RecordTracking("TRK-2", null));
        Assert.Equal("TRK-2", order.Shipment!.TrackingNumber);

        Assert.Equal(BusinessViolationCodes.InvalidOrderTransition, CodeOf(() => Placed().RecordTracking("X", null)));
        Assert.Equal(BusinessViolationCodes.InvalidOrderTransition, CodeOf(() => order.RecordTracking(" ", null)));
    }

    // ── returns and refunds (U-G6, U-G8) ────────────────────────────────────────────────────────────
    private static OrderAggregate Delivered()
    {
        var order = Packing(OrderPaymentMethod.CashOnDelivery);
        order.Dispatch(Ship(), T0.AddHours(2), Staff, Corr);
        order.RecordDelivery(T0.AddHours(3), new CodCollection(1927.50m, CollectorKind.Staff, "Rima", Staff.UserId, T0.AddHours(3), Staff.UserId), null, T0.AddHours(3), Staff, Corr);
        order.RecordPayment(Guid.NewGuid(), "CashOnDelivery", 1927.50m, T0.AddHours(3));
        return order;
    }

    [Fact]
    public void U_G6_ARefund_AboveWhatWasPaid_IsRefused_AnExactFullRefundOnADeliveredOrder_IsRefunded()
    {
        var order = Delivered();
        Assert.Equal(1927.50m, order.AmountPaid);

        Assert.Equal(BusinessViolationCodes.RefundExceedsAmountPaid, CodeOf(() => order.ApplyRefund(1927.51m, T0, Staff, Corr)));
        Assert.Equal(BusinessViolationCodes.RefundExceedsAmountPaid, CodeOf(() => order.ApplyRefund(0m, T0, Staff, Corr)));

        order.ApplyRefund(500m, T0.AddDays(1), Staff, Corr);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(BusinessViolationCodes.RefundExceedsAmountPaid, CodeOf(() => order.ApplyRefund(1427.51m, T0, Staff, Corr)));

        order.ApplyRefund(1427.50m, T0.AddDays(2), Staff, Corr);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(1927.50m, order.AmountRefunded);
        Assert.Single(order.Events.OfType<OrderRefundedEvent>());
    }

    [Fact]
    public void ARefundOnACancelledPaidOrder_ReducesRefundDue_AndTheOrderStaysCancelled()
    {
        var order = Placed(OrderPaymentMethod.BankTransfer);
        order.AwaitTransfer(T0.AddHours(48));
        order.ConfirmPayment(Guid.NewGuid(), "BankTransfer", 1927.50m, T0, Staff, Corr);
        order.Cancel(Customer, true, OrderCancellationReason.CustomerRequest, null, T0.AddMinutes(1), Corr);

        order.ApplyRefund(1000m, T0.AddDays(1), Staff, Corr);
        Assert.Equal(927.50m, order.RefundDue);
        order.ApplyRefund(927.50m, T0.AddDays(1), Staff, Corr);
        Assert.Equal(0m, order.RefundDue);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void U_G8_EveryLineFullyReturnedWhileDelivered_MovesTheOrderToReturned()
    {
        var order = Delivered();
        var rows = order.StatusHistory.Count;

        order.RegisterReturnedQuantity(order.Lines[0].Id, 1, T0.AddDays(1), Staff, Corr);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        order.RegisterReturnedQuantity(order.Lines[0].Id, 1, T0.AddDays(1), Staff, Corr);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        order.RegisterReturnedQuantity(order.Lines[1].Id, 1, T0.AddDays(1), Staff, Corr);

        Assert.Equal(OrderStatus.Returned, order.Status);
        Assert.Equal(rows + 1, order.StatusHistory.Count);
        Assert.Single(order.Events.OfType<OrderReturnedEvent>());
    }

    [Fact]
    public void ReturningMoreThanWasDelivered_OrFromAnOrderNotDelivered_IsRefused()
    {
        var order = Delivered();
        Assert.Equal(BusinessViolationCodes.ReturnQuantityExceeded, CodeOf(() => order.RegisterReturnedQuantity(order.Lines[1].Id, 2, T0, Staff, Corr)));
        Assert.Equal(BusinessViolationCodes.ReturnQuantityExceeded, CodeOf(() => order.RegisterReturnedQuantity(order.Lines[0].Id, 0, T0, Staff, Corr)));
        Assert.Equal(BusinessViolationCodes.ReturnQuantityExceeded, CodeOf(() => order.RegisterReturnedQuantity(Guid.NewGuid(), 1, T0, Staff, Corr)));
        Assert.Equal(BusinessViolationCodes.ReturnQuantityExceeded, CodeOf(() => Placed().RegisterReturnedQuantity(Guid.NewGuid(), 1, T0, Staff, Corr)));
    }

    // ── U-H2: the counter ───────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_H2_CompleteCounterSale_RecordsPendingPaymentConfirmedDelivered_InThreeRows()
    {
        var order = Placed(channel: OrderChannel.Counter);
        order.RecordPayment(Guid.NewGuid(), "CashInStore", 1000m, T0);
        order.RecordPayment(Guid.NewGuid(), "CardInStore", 927.50m, T0);

        order.CompleteCounterSale(0m, T0.AddMinutes(1), Staff, Corr);

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal([OrderStatus.PendingPayment, OrderStatus.Confirmed, OrderStatus.Delivered], order.StatusHistory.Select(h => h.To));
        Assert.Null(order.StatusHistory[0].From);
        Assert.Equal(OrderStatus.PendingPayment, order.StatusHistory[1].From);
        Assert.Equal(OrderStatus.Confirmed, order.StatusHistory[2].From);
        Assert.Single(order.Events.OfType<CounterSaleCompletedEvent>());
        Assert.Equal(T0.AddMinutes(1), order.DeliveredOn);
    }

    [Fact]
    public void ACounterSale_WhoseTendersDoNotCoverTheTotal_IsRefused_AndOnlyACounterSaleCompletesThisWay()
    {
        var order = Placed(channel: OrderChannel.Counter);
        order.RecordPayment(Guid.NewGuid(), "CashInStore", 1927.49m, T0);

        Assert.Equal(BusinessViolationCodes.TendersDoNotCoverTotal, CodeOf(() => order.CompleteCounterSale(0m, T0, Staff, Corr)));
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Throws<InvalidOperationException>(() => Placed().CompleteCounterSale(0m, T0, Staff, Corr));
    }

    // ── customer data ───────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Anonymisation_RedactsATerminalOrderNow_AndAnOpenOneAtItsTerminalTransition()
    {
        var open = Placed();
        Assert.True(open.FlagCustomerAnonymised());
        Assert.False(open.FlagCustomerAnonymised());
        Assert.Equal("Rina Akter", open.Fulfilment.RecipientName); // still open: needed to deliver it

        open.Cancel(Customer, true, OrderCancellationReason.CustomerRequest, null, T0, Corr);
        Assert.Equal("Anonymised customer", open.Fulfilment.RecipientName);
        Assert.Equal("[redacted]", open.Fulfilment.Line1);
        Assert.Equal("[redacted]", open.Fulfilment.RecipientPhone);
        Assert.Equal("Dhaka", open.Fulfilment.District); // kept for reporting

        var terminal = Delivered();
        terminal.FlagCustomerAnonymised();
        Assert.Equal("Anonymised customer", terminal.Fulfilment.RecipientName);
    }

    [Fact]
    public void AMergedCustomer_ReassignsTheOrder_Idempotently()
    {
        var order = Placed();
        var survivor = Guid.NewGuid();

        Assert.True(order.ReassignCustomer(survivor));
        Assert.False(order.ReassignCustomer(survivor));
        Assert.Equal(survivor, order.CustomerId);
    }

    [Fact]
    public void Payments_AreCounted_AndCappedAtTen()
    {
        var order = Placed();
        for (var i = 0; i < OrderRules.MaxPaymentRefs; i++) order.RecordPayment(Guid.NewGuid(), "CashOnDelivery", 1m, T0);

        Assert.Equal(10m, order.AmountPaid);
        Assert.Throws<InvalidOperationException>(() => order.RecordPayment(Guid.NewGuid(), "CashOnDelivery", 1m, T0));
        Assert.Throws<InvalidOperationException>(() => Placed().RecordPayment(Guid.NewGuid(), "CashOnDelivery", 0m, T0));
    }

    [Fact]
    public void AllowedActions_AreTheTransitionTablesNarrowedByTheOrder()
    {
        var order = Placed(OrderPaymentMethod.CashOnDelivery);
        Assert.Equal(new[] { OrderTransitionAction.Cancel, OrderTransitionAction.StartPacking }, order.AllowedActions().OrderBy(a => a).ToArray());
        Assert.Contains(OrderTransitionAction.ConfirmPayment, Placed(OrderPaymentMethod.BankTransfer).AllowedActions());
    }

    [Fact]
    public void Events_CarryNoText_OnlyIdsFlagsEnumValuesAndMoney()
    {
        var order = Packing();
        order.Dispatch(Ship(), T0, Staff, Corr);

        foreach (var domainEvent in order.Events)
        {
            var strings = domainEvent.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(p => p.PropertyType == typeof(string));
            Assert.Empty(strings);
        }
    }
}

