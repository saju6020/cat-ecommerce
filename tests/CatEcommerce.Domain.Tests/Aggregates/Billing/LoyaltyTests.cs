using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Billing;

/// <summary>HO-BIL-001 section 15 group I (U-I1, U-I3, U-I4 at the domain level) -- points are earned on the qualifying net, spent against the
/// balance, and taken back without ever driving the balance negative (US-BIL-10; ADR-191).</summary>
public sealed class LoyaltyTests
{
    private static string ErrorOf(Action act) => Assert.Throws<DomainRuleViolationException>(act).ErrorCode;

    private static LoyaltyAccountAggregate Account(int balance = 0)
    {
        var account = new LoyaltyAccountAggregate(Guid.NewGuid());
        account.Open(BillingTestData.Actor().UserContext);
        if (balance > 0) account.Accrue(balance);
        return account;
    }

    // ── U-I1 ────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(1234.00, 100.00, 12)]
    [InlineData(99.99, 100.00, 0)]
    [InlineData(100.00, 100.00, 1)]
    [InlineData(0, 100.00, 0)]
    [InlineData(-5, 100.00, 0)]
    [InlineData(5000, 0, 0)]
    public void U_I1_AccrualIsTheFloorOfTheQualifyingNetOverTheSpendPerPoint(double net, double spend, int expected) =>
        Assert.Equal(expected, LoyaltyCalculator.AccrualPoints((decimal)net, (decimal)spend));

    [Fact]
    public void AReversal_IsWhatTheCreditedNetEarned_CappedAtWhatTheInvoiceStillHolds()
    {
        Assert.Equal(3, LoyaltyCalculator.ReversalPoints(350m, 100m, 12));
        Assert.Equal(2, LoyaltyCalculator.ReversalPoints(350m, 100m, 2));
        Assert.Equal(0, LoyaltyCalculator.ReversalPoints(350m, 100m, 0));
        Assert.Equal(0, LoyaltyCalculator.ReversalPoints(99m, 100m, 12));
    }

    [Fact]
    public void ARedemption_IsWorthPointsTimesThePointValue_RoundedLikeEveryAmount()
    {
        Assert.Equal(200m, LoyaltyCalculator.RedemptionValue(200, 1m));
        Assert.Equal(12.50m, LoyaltyCalculator.RedemptionValue(25, 0.50m));
        Assert.Equal(0.03m, LoyaltyCalculator.RedemptionValue(3, 0.01m));
    }

    // ── Account ─────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_I3_AnAccount_RefusesToSpendMoreThanItHolds()
    {
        var account = Account(150);

        Assert.Equal(BusinessViolationCodes.LoyaltyInsufficientPoints, ErrorOf(() => account.Redeem(151)));
        Assert.Equal(150, account.Balance);

        account.Redeem(100);
        Assert.Equal(50, account.Balance);
        account.ReverseRedemption(100);
        Assert.Equal(150, account.Balance);
    }

    [Fact]
    public void U_I4_AReversalLargerThanTheBalance_EmptiesIt_AndReportsTheShortfall()
    {
        var account = Account(30);

        var shortfall = account.ReverseAccrual(50);

        Assert.Equal(0, account.Balance);
        Assert.Equal(20, shortfall);
        Assert.Equal(0, Account(100).ReverseAccrual(40));
    }

    [Fact]
    public void AMergedAwayAccount_HandsItsWholeBalanceOver()
    {
        var duplicate = Account(70);
        var survivor = Account(5);

        survivor.TransferIn(duplicate.TransferOut());

        Assert.Equal(0, duplicate.Balance);
        Assert.Equal(75, survivor.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void PointsMustBePositive(int points)
    {
        var account = Account(10);

        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => account.Accrue(points)));
        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => account.Redeem(points)));
        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => account.ReverseAccrual(points)));
    }

    // ── Entry ───────────────────────────────────────────────────────────────────────────────────────
    private static LoyaltyEntryDto Dto(LoyaltyEntryKind kind, int points = 10, Guid? invoice = null, int shortfall = 0) =>
        new(Guid.NewGuid(), kind, points, invoice, null, null, shortfall, DateTimeOffset.UtcNow, BillingTestData.Actor());

    [Fact]
    public void AnEntry_RaisesTheAccrualAndRedemptionEvents_AndOnlyWhenItHasAnInvoice()
    {
        var accrual = new LoyaltyEntryAggregate(Guid.NewGuid());
        accrual.Record(Dto(LoyaltyEntryKind.Accrual, 12, Guid.NewGuid()));
        Assert.Contains(accrual.Events, e => e is LoyaltyPointsAccruedEvent { Points: 12 });
        Assert.True(accrual.IdempotentSource);

        var redemption = new LoyaltyEntryAggregate(Guid.NewGuid());
        redemption.Record(Dto(LoyaltyEntryKind.Redemption, 200, Guid.NewGuid()));
        Assert.Contains(redemption.Events, e => e is LoyaltyPointsRedeemedEvent { Points: 200 });
        Assert.False(redemption.IdempotentSource); // a redemption may be applied, removed and applied again on one draft

        var merge = new LoyaltyEntryAggregate(Guid.NewGuid());
        merge.Record(Dto(LoyaltyEntryKind.MergeTransferIn));
        Assert.Empty(merge.Events);
        Assert.True(merge.IdempotentSource);
    }

    [Fact]
    public void AnEntry_NeedsPositivePoints_AndAShortfallWithinThem()
    {
        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => new LoyaltyEntryAggregate(Guid.NewGuid()).Record(Dto(LoyaltyEntryKind.Accrual, 0))));
        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => new LoyaltyEntryAggregate(Guid.NewGuid()).Record(Dto(LoyaltyEntryKind.AccrualReversal, 5, shortfall: 6))));
        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => new LoyaltyEntryAggregate(Guid.NewGuid()).Record(Dto(LoyaltyEntryKind.AccrualReversal, 5, shortfall: -1))));
    }
}
