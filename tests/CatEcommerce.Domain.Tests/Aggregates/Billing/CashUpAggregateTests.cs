using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Billing;

/// <summary>HO-BIL-001 section 15 group F (U-F1, U-F2, U-F4) -- the till count: expected cash is the calculator's, a variance needs an
/// explanation, and a closed day is terminal (US-BIL-12, US-BIL-06 AC3; ADR-188).</summary>
public sealed class CashUpAggregateTests
{
    private static readonly DateOnly Day = new(2026, 10, 7);
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static string ErrorOf(Action act) => Assert.Throws<DomainRuleViolationException>(act).ErrorCode;

    private static CashUpRow Pay(PaymentMethod method, decimal amount, CollectorKind? collector = null, bool carried = false) =>
        new(Guid.NewGuid(), CashUpRowKind.Payment, method, collector, null, amount, carried);

    private static CashUpAggregate Open()
    {
        var cashUp = new CashUpAggregate(CashUpIds.For(Guid.NewGuid(), Day));
        cashUp.Open(Guid.NewGuid(), Day, BillingTestData.Actor().UserContext);
        return cashUp;
    }

    [Fact]
    public void U_F1_TheCalculator_CountsCashAndStaffCod_AndListsCardAndTransfer()
    {
        var rows = new[]
        {
            Pay(PaymentMethod.CashInStore, 500m), Pay(PaymentMethod.CashInStore, 500m), Pay(PaymentMethod.CashInStore, 500m),
            Pay(PaymentMethod.CashOnDelivery, 200m, CollectorKind.Staff), Pay(PaymentMethod.CashOnDelivery, 900m, CollectorKind.Courier),
            Pay(PaymentMethod.CardInStore, 700m), Pay(PaymentMethod.BankTransfer, 1000m),
            new CashUpRow(Guid.NewGuid(), CashUpRowKind.Refund, null, null, RefundMethod.CashInStore, 100m, false),
            new CashUpRow(Guid.NewGuid(), CashUpRowKind.Reversal, PaymentMethod.CashInStore, null, null, 50m, false),
            Pay(PaymentMethod.CashInStore, 20m, carried: true),
        };

        var snapshot = CashUpCalculator.Calculate(rows);

        Assert.Equal(1570m, snapshot.ExpectedCash);
        Assert.Equal(700m, snapshot.CardTotal);
        Assert.Equal(1000m, snapshot.TransferTotal);
        Assert.Equal(1700m, snapshot.CashTotal); // 3 x 500 cash + 200 staff-collected COD
        Assert.Equal(100m, snapshot.CashRefunds);
        Assert.Equal(50m, snapshot.CashReversals);
        Assert.Equal(20m, snapshot.CarriedIn);
    }

    [Fact]
    public void U_F1_ABankTransferRefund_AndACourierReversal_DoNotTouchTheTill()
    {
        var snapshot = CashUpCalculator.Calculate(
        [
            Pay(PaymentMethod.CashInStore, 300m),
            new CashUpRow(Guid.NewGuid(), CashUpRowKind.Refund, null, null, RefundMethod.BankTransfer, 100m, false),
            new CashUpRow(Guid.NewGuid(), CashUpRowKind.Reversal, PaymentMethod.CashOnDelivery, CollectorKind.Courier, null, 40m, false),
        ]);

        Assert.Equal(300m, snapshot.ExpectedCash);
        Assert.Equal(0m, snapshot.CashRefunds);
        Assert.Equal(0m, snapshot.CashReversals);
    }

    [Fact]
    public void U_F2_ANonZeroVariance_NeedsAnExplanation_ToClose()
    {
        var snapshot = CashUpCalculator.Calculate([Pay(PaymentMethod.CashInStore, 100m)]);
        var cashUp = Open();
        cashUp.RecordCount(90m, null, Day, Now, BillingTestData.Actor());

        Assert.Equal(BusinessViolationCodes.VarianceExplanationRequired, ErrorOf(() => cashUp.Close(snapshot, Now, BillingTestData.Actor())));
        Assert.Equal(CashUpStatus.Open, cashUp.Status);

        cashUp.RecordCount(90m, "Ten taka given as change by mistake", Day, Now, BillingTestData.Actor());
        cashUp.Close(snapshot, Now, BillingTestData.Actor());

        Assert.Equal(CashUpStatus.Closed, cashUp.Status);
        Assert.Equal(-10m, cashUp.Variance);
        Assert.Equal(100m, cashUp.ExpectedSnapshot!.ExpectedCash);
        var closed = Assert.IsType<CashUpClosedEvent>(Assert.Single(cashUp.Events));
        Assert.Equal(-10m, closed.Variance);
    }

    [Fact]
    public void U_F2_AMatchingCount_ClosesWithoutAnExplanation_AndAnUncountedDayCannotClose()
    {
        var snapshot = CashUpCalculator.Calculate([Pay(PaymentMethod.CashInStore, 100m)]);
        var uncounted = Open();
        Assert.Equal(BusinessViolationCodes.CountRequired, ErrorOf(() => uncounted.Close(snapshot, Now, BillingTestData.Actor())));

        var cashUp = Open();
        cashUp.RecordCount(100m, null, Day, Now, BillingTestData.Actor());
        cashUp.Close(snapshot, Now, BillingTestData.Actor());
        Assert.Equal(0m, cashUp.Variance);
    }

    [Fact]
    public void U_F4_AFutureDayCannotBeCounted_AndAClosedDayCannotBeRecountedOrClosedAgain()
    {
        var future = Open();
        Assert.Equal(BusinessViolationCodes.CashUpDateInFuture, ErrorOf(() => future.RecordCount(10m, null, Day.AddDays(-1), Now, BillingTestData.Actor())));

        var cashUp = Open();
        cashUp.RecordCount(0m, null, Day, Now, BillingTestData.Actor());
        cashUp.Close(CashUpCalculator.Calculate([]), Now, BillingTestData.Actor());

        Assert.Equal(BusinessViolationCodes.CashUpClosed, ErrorOf(() => cashUp.RecordCount(5m, null, Day, Now, BillingTestData.Actor())));
        Assert.Equal(BusinessViolationCodes.CashUpClosed, ErrorOf(() => cashUp.Close(CashUpCalculator.Calculate([]), Now, BillingTestData.Actor())));
    }

    [Fact]
    public void ARecount_ReplacesTheEarlierCount_AndTheCountMustBeAMoneyAmount()
    {
        var cashUp = Open();
        cashUp.RecordCount(10m, "first", Day, Now, BillingTestData.Actor());
        cashUp.RecordCount(20m, null, Day, Now, BillingTestData.Actor());

        Assert.Equal(20m, cashUp.CountedCash);
        Assert.Null(cashUp.Explanation);
        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => cashUp.RecordCount(-1m, null, Day, Now, BillingTestData.Actor())));
        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => cashUp.RecordCount(1.005m, null, Day, Now, BillingTestData.Actor())));
    }

    [Fact]
    public void TheCashUpId_IsTheSameForTheSameBranchAndDay_AndDifferentOtherwise()
    {
        var branch = Guid.NewGuid();
        Assert.Equal(CashUpIds.For(branch, Day), CashUpIds.For(branch, Day));
        Assert.NotEqual(CashUpIds.For(branch, Day), CashUpIds.For(branch, Day.AddDays(1)));
        Assert.NotEqual(CashUpIds.For(branch, Day), CashUpIds.For(Guid.NewGuid(), Day));
    }
}
