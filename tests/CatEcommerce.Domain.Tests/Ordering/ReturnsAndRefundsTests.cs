using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Tests.Ordering;

/// <summary>HO-ORD-001 Phase G units U-G1 to U-G8 -- the return eligibility policy (BR-10, the window, quantities), the refund method table, the cumulative-rounding refund
/// calculation and the return request's state machine.</summary>
public sealed class ReturnsAndRefundsTests
{
    private static readonly UserContext Staff = new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Reception"] };
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Delivered = new(2026, 10, 1);

    private static ReturnLineEligibilityInput Line(
        ProductReturnClass? returnClass = null, ItemCondition? condition = null, ReturnReason? reason = null, int ordered = 3, int returned = 0, int open = 0, int requested = 1, bool recalled = false) =>
        new(Guid.NewGuid(), "Kitten food", ordered, returned, open, returnClass ?? ProductReturnClass.Food, recalled, requested, reason ?? ReturnReason.ChangedMind, condition ?? ItemCondition.Sealed);

    private static ReturnLineVerdict Judge(ReturnLineEligibilityInput line, int today = 5, OrderStatus? status = null, DateOnly? delivered = null, int window = 7) =>
        Assert.Single(ReturnEligibilityPolicy.Evaluate(new ReturnEligibilityInput(status ?? OrderStatus.Delivered, delivered ?? Delivered, new DateOnly(2026, 10, today), window, [line])));

    // U-G1 the window
    [Theory]
    [InlineData(8, true)]    // exactly delivered + 7 days is still inside
    [InlineData(9, false)]
    public void TheWindow_IsInclusiveOfTheLastDay(int today, bool eligible)
    {
        var verdict = Judge(Line(ProductReturnClass.Other), today);
        Assert.Equal(eligible, verdict.Eligible);
        if (!eligible) Assert.Equal(BusinessViolationCodes.ReturnWindowClosed, verdict.Code);
    }

    [Fact]
    public void AnOrderThatWasNotDelivered_CannotBeReturned()
    {
        Assert.Equal(BusinessViolationCodes.ReturnWindowClosed, Judge(Line(), status: OrderStatus.Dispatched).Code);
        Assert.Equal(BusinessViolationCodes.ReturnWindowClosed, Assert.Single(ReturnEligibilityPolicy.Evaluate(
            new ReturnEligibilityInput(OrderStatus.Delivered, null, new DateOnly(2026, 10, 5), 7, [Line()]))).Code);
    }

    // U-G2 quantities
    [Theory]
    [InlineData(3, 1, 1, 1, true)]
    [InlineData(3, 1, 1, 2, false)]   // 3 - 1 returned - 1 open = 1 left
    [InlineData(3, 3, 0, 1, false)]
    [InlineData(3, 0, 0, 0, false)]
    public void TheQuantity_IsWhatIsLeftAfterReturnsAndOpenRequests(int ordered, int returned, int open, int requested, bool eligible)
    {
        var verdict = Judge(Line(ProductReturnClass.Other, ordered: ordered, returned: returned, open: open, requested: requested));
        Assert.Equal(eligible, verdict.Eligible);
        if (!eligible) Assert.Equal(BusinessViolationCodes.ReturnQuantityExceeded, verdict.Code);
    }

    // U-G3 BR-10
    [Fact]
    public void OpenedFoodForAChangeOfMind_IsRefused_ButSealedFoodIsNot()
    {
        Assert.Equal(BusinessViolationCodes.ReturnNotEligibleOpenedFood, Judge(Line(condition: ItemCondition.Opened)).Code);
        Assert.Equal(BusinessViolationCodes.ReturnNotEligibleOpenedFood, Judge(Line(condition: ItemCondition.Used)).Code);
        var sealedFood = Judge(Line());
        Assert.True(sealedFood.Eligible);
        Assert.False(sealedFood.RequiresDefectConfirmation);
    }

    [Fact]
    public void UsedLitter_IsRefused_ButOpenedLitterIsNot()
    {
        Assert.Equal(BusinessViolationCodes.ReturnNotEligibleUsedLitter, Judge(Line(ProductReturnClass.Litter, ItemCondition.Used)).Code);
        Assert.True(Judge(Line(ProductReturnClass.Litter, ItemCondition.Opened)).Eligible);
    }

    // U-G4 the exceptions
    [Fact]
    public void ARecalledBatch_MakesOpenedFoodEligible_WithoutADefectToConfirm()
    {
        var verdict = Judge(Line(condition: ItemCondition.Opened, recalled: true));
        Assert.True(verdict.Eligible);
        Assert.False(verdict.RequiresDefectConfirmation);
    }

    [Fact]
    public void AQualityDefectClaim_IsEligibleForApproval_WithTheDefectToConfirm()
    {
        var verdict = Judge(Line(condition: ItemCondition.Opened, reason: ReturnReason.QualityDefect));
        Assert.True(verdict.Eligible);
        Assert.True(verdict.RequiresDefectConfirmation);
    }

    [Fact]
    public void OtherProducts_AreNotSubjectToBr10_InAnyCondition() =>
        Assert.True(Judge(Line(ProductReturnClass.Other, ItemCondition.Used)).Eligible);

    // U-G5 refund methods
    [Theory]
    [InlineData("CashOnDelivery", true, true)]
    [InlineData("CashInStore", true, true)]
    [InlineData("CardInStore", true, true)]
    [InlineData("BankTransfer", false, true)]
    public void RefundMethods_FollowHowTheCustomerPaid(string paid, bool cash, bool transfer)
    {
        var options = RefundMethodPolicy.Evaluate([paid], storeCreditAvailable: false).ToDictionary(o => o.Method);
        Assert.Equal(cash, options[OrderRefundMethod.CashInStore].AllowedForPayment);
        Assert.Equal(transfer, options[OrderRefundMethod.BankTransfer].AllowedForPayment);
    }

    [Fact]
    public void StoreCredit_IsAllowedButUnavailable_UntilBillingOffersIt()
    {
        var unavailable = RefundMethodPolicy.Evaluate(["CashOnDelivery"], false).Single(o => o.Method == OrderRefundMethod.StoreCredit);
        Assert.True(unavailable.AllowedForPayment);
        Assert.False(unavailable.Available);
        Assert.Equal(RefundMethodPolicy.StoreCreditUnavailableReason, unavailable.UnavailableReason);
        Assert.True(RefundMethodPolicy.Evaluate(["CashOnDelivery"], true).Single(o => o.Method == OrderRefundMethod.StoreCredit).Available);
    }

    [Fact]
    public void SeveralTenders_AllowTheUnion_AndNoPaymentAllowsNothing()
    {
        var both = RefundMethodPolicy.Evaluate(["BankTransfer", "CashInStore"], false).Where(o => o.AllowedForPayment).Select(o => o.Method).ToList();
        Assert.Contains(OrderRefundMethod.CashInStore, both);
        Assert.Contains(OrderRefundMethod.BankTransfer, both);
        Assert.All(RefundMethodPolicy.Evaluate([], false), o => Assert.False(o.AllowedForPayment));
    }

    // U-G6 cumulative rounding
    [Fact]
    public void RefundingEveryUnitOfALine_AddsUpToTheLineTotalExactly()
    {
        var total = MinorUnits.FromAmount(100.00m);
        var sum = MinorUnits.Zero;
        for (var returned = 0; returned < 3; returned++)
            sum += RefundCalculator.ValueOf(new RefundLineInput(total, 3, returned, 1));

        Assert.Equal(total, sum);   // 33.33 + 33.34 + 33.33
    }

    [Fact]
    public void TheSuggestion_SumsTheLines_AndIgnoresLinesNotReturned()
    {
        var suggested = RefundCalculator.Suggested([
            new RefundLineInput(MinorUnits.FromAmount(1955.00m), 2, 0, 1),
            new RefundLineInput(MinorUnits.FromAmount(120.00m), 1, 0, 1),
            new RefundLineInput(MinorUnits.FromAmount(50.00m), 1, 0, 0)]);

        Assert.Equal(MinorUnits.FromAmount(977.50m + 120.00m), suggested);
    }

    // U-G7 / U-G8 the return request
    private static (ReturnRequestAggregate Request, Guid LineId) Requested(ReturnKind? kind = null, bool defect = false)
    {
        var lineId = Guid.NewGuid();
        var request = new ReturnRequestAggregate(Guid.NewGuid());
        request.Request(new ReturnRequestDto(Guid.NewGuid(), Guid.NewGuid(), kind ?? ReturnKind.CustomerReturn,
            [new ReturnLineDto(lineId, 2, ReturnReason.QualityDefect, ItemCondition.Opened, " mouldy ", defect)], Staff.UserId, T0, Staff, Guid.NewGuid()));
        return (request, lineId);
    }

    private static void Receive(ReturnRequestAggregate request, Guid lineId, int quantity) =>
        request.Receive(Staff.UserId, [new ReturnReceiptDto(lineId, quantity, [new ReturnReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), quantity)])], T0, Staff, Guid.NewGuid());

    [Fact]
    public void ACustomerReturn_StartsRequested_AndADeliveryFailureStartsApproved()
    {
        Assert.Equal(ReturnStatus.Requested, Requested().Request.Status);
        var failure = Requested(ReturnKind.DeliveryFailure).Request;
        Assert.Equal(ReturnStatus.Approved, failure.Status);
        Assert.Equal(Staff.UserId, failure.DecidedBy);
    }

    [Fact]
    public void ADefectLine_CannotBeApproved_WithoutTheDefectConfirmedAndNoted()
    {
        var (request, lineId) = Requested(defect: true);

        Assert.Equal(BusinessViolationCodes.DefectConfirmationRequired,
            Assert.Throws<DomainRuleViolationException>(() => request.Approve(Staff.UserId, [], null, T0, Staff, Guid.NewGuid())).ErrorCode);
        Assert.Equal(BusinessViolationCodes.DefectConfirmationRequired,
            Assert.Throws<DomainRuleViolationException>(() => request.Approve(Staff.UserId, [new DefectConfirmationDto(lineId, true, "  ")], null, T0, Staff, Guid.NewGuid())).ErrorCode);

        request.Approve(Staff.UserId, [new DefectConfirmationDto(lineId, true, "Mould")], null, T0, Staff, Guid.NewGuid());
        Assert.Equal(ReturnStatus.Approved, request.Status);
        Assert.Equal("Mould", request.Lines.Single().DefectNote);
    }

    [Fact]
    public void TheStateMachine_AllowsEachStepOnce_InOrder()
    {
        var (request, lineId) = Requested();
        Assert.Throws<DomainRuleViolationException>(() => Receive(request, lineId, 1));   // not approved yet

        request.Approve(Staff.UserId, [], null, T0, Staff, Guid.NewGuid());
        Assert.Throws<DomainRuleViolationException>(() => request.Reject(Staff.UserId, "no", T0, Staff, Guid.NewGuid()));
        Assert.Throws<DomainRuleViolationException>(() => Receive(request, lineId, 3));   // more than approved
        Receive(request, lineId, 2);
        Assert.Equal(ReturnStatus.Received, request.Status);
        Assert.Throws<DomainRuleViolationException>(() => Receive(request, lineId, 1));   // once
    }

    [Fact]
    public void RejectingNeedsAReason_AndIsFinal()
    {
        var (request, _) = Requested();
        Assert.Throws<DomainRuleViolationException>(() => request.Reject(Staff.UserId, " ", T0, Staff, Guid.NewGuid()));
        request.Reject(Staff.UserId, "Signs of use", T0, Staff, Guid.NewGuid());
        Assert.Equal(ReturnStatus.Rejected, request.Status);
        Assert.Throws<DomainRuleViolationException>(() => request.Approve(Staff.UserId, [], null, T0, Staff, Guid.NewGuid()));
    }

    [Fact]
    public void AReturnWithNoLines_OrADuplicateLine_IsRefused()
    {
        var request = new ReturnRequestAggregate(Guid.NewGuid());
        Assert.Throws<DomainRuleViolationException>(() => request.Request(new ReturnRequestDto(Guid.NewGuid(), null, ReturnKind.CustomerReturn, [], Staff.UserId, T0, Staff, Guid.NewGuid())));

        var id = Guid.NewGuid();
        var line = new ReturnLineDto(id, 1, ReturnReason.Other, ItemCondition.Sealed, null, false);
        Assert.Throws<DomainRuleViolationException>(() => new ReturnRequestAggregate(Guid.NewGuid())
            .Request(new ReturnRequestDto(Guid.NewGuid(), null, ReturnKind.CustomerReturn, [line, line], Staff.UserId, T0, Staff, Guid.NewGuid())));
    }
}
