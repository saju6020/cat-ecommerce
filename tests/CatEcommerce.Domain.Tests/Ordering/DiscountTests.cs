using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Tests.Ordering;

/// <summary>HO-ORD-001 §15 U-B6…U-B10 -- discount codes (US-ORD-05, ADR-207).</summary>
public sealed class DiscountTests
{
    private static readonly UserContext User = new(Guid.NewGuid(), Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Litter1 = Guid.NewGuid();
    private static readonly Guid Litter2 = Guid.NewGuid();
    private static readonly Guid Food1 = Guid.NewGuid();
    private static readonly int LitterCategory = ProductCategory.Litter.Value;
    private static readonly int FoodCategory = ProductCategory.CatFood.Value;

    private static DiscountRuleSet Rules(
        DiscountKind? kind = null, long? percentHundredths = 1000, long? amount = null, long? minimum = null, DiscountScope? scope = null,
        Guid[]? products = null, int[]? categories = null, bool firstOrder = false, int? usageLimit = null, int used = 0, int? perCustomer = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null, bool active = true) =>
        new(Guid.NewGuid(), "SAVE10", kind ?? DiscountKind.Percentage,
            (kind ?? DiscountKind.Percentage) == DiscountKind.Percentage ? percentHundredths : null,
            amount is { } a ? new MinorUnits(a) : null,
            minimum is { } m ? new MinorUnits(m) : null,
            scope ?? DiscountScope.Order, (products ?? []).ToHashSet(), (categories ?? []).ToHashSet(), firstOrder, usageLimit, used, perCustomer, from, to, active, "BDT");

    private static DiscountCartLine Line(Guid productId, int category, long subtotal) => new(Guid.NewGuid(), productId, category, new MinorUnits(subtotal));

    private static readonly CustomerDiscountFacts NewCustomer = new(0, false);

    // ── U-B6: each failing rule, in order ───────────────────────────────────────────────────────────
    [Fact]
    public void U_B6_EachFailingRule_ReturnsItsCodeAndAPlainMessage_InTheDocumentedOrder()
    {
        var lines = new[] { Line(Food1, FoodCategory, 100_000) };

        void Expect(string code, DiscountRuleSet? rules, CustomerDiscountFacts? customer = null)
        {
            var result = DiscountRules.Evaluate(rules, lines, customer ?? NewCustomer, Now);
            Assert.False(result.Applied);
            Assert.Equal(code, result.ReasonCode);
            Assert.False(string.IsNullOrWhiteSpace(result.Message));
        }

        Expect("DiscountCodeNotFound", null);
        Expect("DiscountCodeInactive", Rules(active: false));
        Expect("DiscountCodeNotYetValid", Rules(from: Now.AddDays(1)));
        Expect("DiscountCodeExpired", Rules(to: Now.AddSeconds(-1)));
        Expect("DiscountUsageLimitReached", Rules(usageLimit: 5, used: 5));
        Expect("DiscountCustomerLimitReached", Rules(perCustomer: 1), new CustomerDiscountFacts(1, true));
        Expect("DiscountFirstOrderOnly", Rules(firstOrder: true), new CustomerDiscountFacts(0, true));
        Expect("DiscountNotApplicableToItems", Rules(scope: DiscountScope.Categories, categories: [LitterCategory]));
        Expect("DiscountMinimumSpendNotMet", Rules(minimum: 150_000));
    }

    [Fact]
    public void U_B6_WhenSeveralRulesFail_TheFirstInOrderWins()
    {
        var lines = new[] { Line(Food1, FoodCategory, 100) };
        var allWrong = Rules(active: false, from: Now.AddDays(1), usageLimit: 1, used: 1, firstOrder: true, minimum: 999_999);

        Assert.Equal("DiscountCodeInactive", DiscountRules.Evaluate(allWrong, lines, new CustomerDiscountFacts(9, true), Now).ReasonCode);
        Assert.Equal("DiscountCodeNotYetValid", DiscountRules.Evaluate(allWrong with { IsActive = true }, lines, new CustomerDiscountFacts(9, true), Now).ReasonCode);
        Assert.Equal("DiscountUsageLimitReached", DiscountRules.Evaluate(allWrong with { IsActive = true, ValidFrom = null }, lines, new CustomerDiscountFacts(9, true), Now).ReasonCode);
    }

    [Fact]
    public void U_B6_TheMinimumSpendMessage_NamesTheAmount()
    {
        var result = DiscountRules.Evaluate(Rules(minimum: 150_000), [Line(Food1, FoodCategory, 100_000)], NewCustomer, Now);

        Assert.Equal("Spend BDT 1,500.00 on eligible items to use this code.", result.Message);
        Assert.Equal("This code is for a first order only.", DiscountRules.Evaluate(Rules(firstOrder: true), [Line(Food1, FoodCategory, 1)], new CustomerDiscountFacts(0, true), Now).Message);
    }

    [Fact]
    public void U_B6_TheMinimumSpend_CountsEligibleItemsOnly()
    {
        var rules = Rules(scope: DiscountScope.Categories, categories: [LitterCategory], minimum: 50_000);
        var lines = new[] { Line(Litter1, LitterCategory, 30_000), Line(Food1, FoodCategory, 500_000) };

        Assert.Equal("DiscountMinimumSpendNotMet", DiscountRules.Evaluate(rules, lines, NewCustomer, Now).ReasonCode);
    }

    [Fact]
    public void U_B6_TheValidityWindow_IsInclusiveAtBothEnds()
    {
        var lines = new[] { Line(Food1, FoodCategory, 100) };

        Assert.True(DiscountRules.Evaluate(Rules(from: Now, to: Now), lines, NewCustomer, Now).Applied);
        Assert.True(DiscountRules.Evaluate(Rules(usageLimit: 5, used: 4), lines, NewCustomer, Now).Applied);
    }

    // ── U-B7 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_B7_A10PercentCodeScopedToLitter_DiscountsLitterLinesOnly_OrderDiscountIsZero()
    {
        var litterA = Line(Litter1, LitterCategory, 85_000);
        var litterB = Line(Litter2, LitterCategory, 12_050);
        var food = Line(Food1, FoodCategory, 200_000);

        var result = DiscountRules.Evaluate(Rules(scope: DiscountScope.Categories, categories: [LitterCategory]), [litterA, litterB, food], NewCustomer, Now);

        Assert.True(result.Applied);
        Assert.Equal(8_500L, result.Application.LineDiscounts[litterA.LineRef].Value);
        Assert.Equal(1_205L, result.Application.LineDiscounts[litterB.LineRef].Value);   // 1205.0 exactly
        Assert.False(result.Application.LineDiscounts.ContainsKey(food.LineRef));
        Assert.Equal(0L, result.Application.OrderDiscount.Value);
        Assert.Empty(result.Application.OrderDiscountShares);
    }

    [Fact]
    public void U_B7_AProductScopedCode_DiscountsThoseProductsOnly()
    {
        var wanted = Line(Litter1, LitterCategory, 10_000);
        var other = Line(Litter2, LitterCategory, 10_000);

        var result = DiscountRules.Evaluate(Rules(scope: DiscountScope.Products, products: [Litter1]), [wanted, other], NewCustomer, Now);

        Assert.Equal(1_000L, result.Application.LineDiscounts[wanted.LineRef].Value);
        Assert.False(result.Application.LineDiscounts.ContainsKey(other.LineRef));
    }

    [Fact]
    public void U_B7_AScopedFixedCode_IsApportionedOverTheMatchingLines_NeverBeyondTheirSubtotals()
    {
        var a = Line(Litter1, LitterCategory, 6_000);
        var b = Line(Litter2, LitterCategory, 4_000);
        var food = Line(Food1, FoodCategory, 90_000);

        var result = DiscountRules.Evaluate(
            Rules(DiscountKind.FixedAmount, amount: 100_000, scope: DiscountScope.Categories, categories: [LitterCategory]), [a, b, food], NewCustomer, Now);

        // the fixed value (1000.00) exceeds the matching goods (100.00): capped, so each line is discounted by its whole subtotal
        Assert.Equal(6_000L, result.Application.LineDiscounts[a.LineRef].Value);
        Assert.Equal(4_000L, result.Application.LineDiscounts[b.LineRef].Value);

        var split = DiscountRules.Evaluate(
            Rules(DiscountKind.FixedAmount, amount: 5_001, scope: DiscountScope.Categories, categories: [LitterCategory]), [a, b, food], NewCustomer, Now);
        Assert.Equal(5_001L, split.Application.LineDiscountTotal.Value);
        Assert.All(split.Application.LineDiscounts, kv => Assert.True(kv.Value.Value <= new[] { a, b }.Single(l => l.LineRef == kv.Key).Subtotal.Value));
    }

    // ── U-B8 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_B8_AFixed100UnscopedOnThreeLines_IsAnOrderDiscountApportionedExactlyIntoShares()
    {
        var lines = new[] { Line(Food1, FoodCategory, 85_000), Line(Litter1, LitterCategory, 85_000), Line(Litter2, LitterCategory, 12_000) };

        var result = DiscountRules.Evaluate(Rules(DiscountKind.FixedAmount, amount: 10_000), lines, NewCustomer, Now);

        Assert.True(result.Applied);
        Assert.Equal(10_000L, result.Application.OrderDiscount.Value);
        Assert.Equal(10_000L, result.Application.OrderDiscountShares.Values.Sum(v => v.Value));
        Assert.Equal(3, result.Application.OrderDiscountShares.Count);
        Assert.Empty(result.Application.LineDiscounts);
    }

    [Fact]
    public void U_B8_AFixedCode_NeverDiscountsMoreThanTheGoods()
    {
        var result = DiscountRules.Evaluate(Rules(DiscountKind.FixedAmount, amount: 1_000_000), [Line(Food1, FoodCategory, 5_000)], NewCustomer, Now);
        Assert.Equal(5_000L, result.Application.OrderDiscount.Value);
    }

    [Fact]
    public void U_B8_A10PercentOrderCode_OnTheWorkedCart_Is182()
    {
        // 2 x 850.00 + 1 x 120.00 = 1820.00; 10 % = 182.00 (U-D1)
        var lines = new[] { Line(Food1, FoodCategory, 170_000), Line(Litter1, LitterCategory, 12_000) };

        var result = DiscountRules.Evaluate(Rules(), lines, NewCustomer, Now);

        Assert.Equal(18_200L, result.Application.OrderDiscount.Value);
        Assert.Equal(18_200L, result.Application.OrderDiscountShares.Values.Sum(v => v.Value));
    }

    // ── U-B9 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_B9_FirstOrderOnly_AppliesAfterACancelledOrder_FailsAfterADeliveredOne()
    {
        var lines = new[] { Line(Food1, FoodCategory, 10_000) };
        var rules = Rules(firstOrder: true);

        // the loader sets HasPriorNonCancelledOrder from the customer's orders: a cancelled order does not count
        Assert.True(DiscountRules.Evaluate(rules, lines, new CustomerDiscountFacts(0, HasPriorNonCancelledOrder: false), Now).Applied);

        var delivered = DiscountRules.Evaluate(rules, lines, new CustomerDiscountFacts(0, HasPriorNonCancelledOrder: true), Now);
        Assert.False(delivered.Applied);
        Assert.Equal("DiscountFirstOrderOnly", delivered.ReasonCode);
    }

    // ── U-B10 ───────────────────────────────────────────────────────────────────────────────────────
    private static DiscountDefinitionDto Definition(
        string code = "t-123", DiscountKind? kind = null, decimal? percent = 10m, decimal? amount = null, int? usageLimit = null, DiscountScope? scope = null,
        Guid[]? products = null, int[]? categories = null) =>
        new(code, "desc", kind ?? DiscountKind.Percentage, (kind ?? DiscountKind.Percentage) == DiscountKind.Percentage ? percent : null, amount, "BDT", null,
            scope ?? DiscountScope.Order, products ?? [], categories ?? [], false, usageLimit, null, null, null, true, User, Guid.NewGuid());

    [Fact]
    public void U_B10_Redeem_AtTheUsageLimit_IsRefused_ReleaseAtZero_StaysZero()
    {
        var code = new DiscountCodeAggregate(Guid.NewGuid());
        code.Create(Definition(usageLimit: 1));

        code.ReleaseRedemption();
        Assert.Equal(0, code.TimesRedeemed);

        code.Redeem();
        Assert.Equal(1, code.TimesRedeemed);

        var ex = Assert.Throws<DomainRuleViolationException>(code.Redeem);
        Assert.Equal(BusinessViolationCodes.DiscountUsageLimitReached, ex.ErrorCode);
        Assert.Equal(1, code.TimesRedeemed);

        code.ReleaseRedemption();
        code.ReleaseRedemption();
        Assert.Equal(0, code.TimesRedeemed);
    }

    [Fact]
    public void ACodeWithoutALimit_CanBeRedeemedWithoutBound()
    {
        var code = new DiscountCodeAggregate(Guid.NewGuid());
        code.Create(Definition());
        for (var i = 0; i < 25; i++) code.Redeem();
        Assert.Equal(25, code.TimesRedeemed);
    }

    [Fact]
    public void TheCode_IsStoredUpperCase_AndCannotBeRenamed()
    {
        var code = new DiscountCodeAggregate(Guid.NewGuid());
        code.Create(Definition("  t-abc "));
        Assert.Equal("T-ABC", code.Code);

        code.Update(Definition("T-ABC", percent: 20m));
        Assert.Equal(20m, code.Percent);

        var ex = Assert.Throws<DomainRuleViolationException>(() => code.Update(Definition("OTHER")));
        Assert.Equal(BusinessViolationCodes.InvalidDiscountDefinition, ex.ErrorCode);
    }

    [Fact]
    public void TheUsageLimit_CannotBeLoweredBelowTheUsesAlreadyMade()
    {
        var code = new DiscountCodeAggregate(Guid.NewGuid());
        code.Create(Definition("T-LIM", usageLimit: 5));
        code.Redeem();
        code.Redeem();
        code.Redeem();

        Assert.Throws<DomainRuleViolationException>(() => code.Update(Definition("T-LIM", usageLimit: 2)));
        code.Update(Definition("T-LIM", usageLimit: 3));
        Assert.Equal(3, code.UsageLimit);
    }

    [Theory]
    [InlineData("AB")]                    // too short
    [InlineData("has space")]
    [InlineData("under_score")]
    [InlineData("THIS-CODE-IS-FAR-TOO-LONG-FOR-THE-RULE")]
    public void ACodeText_ThatBreaksTheShape_IsRefused(string text) =>
        Assert.Equal(BusinessViolationCodes.InvalidDiscountDefinition,
            Assert.Throws<DomainRuleViolationException>(() => new DiscountCodeAggregate(Guid.NewGuid()).Create(Definition(text))).ErrorCode);

    [Fact]
    public void TheDefinition_MustMatchItsKindAndScope()
    {
        DomainRuleViolationException Create(DiscountDefinitionDto dto) => Assert.Throws<DomainRuleViolationException>(() => new DiscountCodeAggregate(Guid.NewGuid()).Create(dto));

        Assert.Equal("InvalidDiscountDefinition", Create(Definition(percent: 0m)).ErrorCode);
        Assert.Equal("InvalidDiscountDefinition", Create(Definition(percent: 100.01m)).ErrorCode);
        Assert.Equal("InvalidDiscountDefinition", Create(Definition(percent: 10.005m)).ErrorCode);
        Assert.Equal("InvalidDiscountDefinition", Create(Definition(percent: null)).ErrorCode);
        Assert.Equal("InvalidDiscountDefinition", Create(Definition(kind: DiscountKind.FixedAmount, amount: null)).ErrorCode);
        Assert.Equal("InvalidDiscountDefinition", Create(Definition(kind: DiscountKind.FixedAmount, amount: 0m)).ErrorCode);
        Assert.Equal("InvalidDiscountDefinition", Create(Definition(kind: DiscountKind.FixedAmount, amount: 10.5m) with { Percent = 5m }).ErrorCode);
        Assert.Equal("InvalidDiscountDefinition", Create(Definition(scope: DiscountScope.Products)).ErrorCode);
        Assert.Equal("InvalidDiscountDefinition", Create(Definition(scope: DiscountScope.Categories, categories: [999])).ErrorCode);
        Assert.Equal("InvalidDiscountDefinition", Create(Definition(products: [Guid.NewGuid()])).ErrorCode);
        Assert.Equal("InvalidDiscountDefinition", Create(Definition(usageLimit: 0)).ErrorCode);
        Assert.Equal("InvalidDiscountDefinition", Create(Definition() with { ValidFrom = Now, ValidTo = Now.AddDays(-1) }).ErrorCode);
    }

    [Fact]
    public void AValidityWindow_IsStoredInUtc()
    {
        var code = new DiscountCodeAggregate(Guid.NewGuid());
        code.Create(Definition() with { ValidFrom = DateTimeOffset.Parse("2026-10-09T10:00:00+06:00") });

        Assert.Equal(TimeSpan.Zero, code.ValidFrom!.Value.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 4, 0, 0, TimeSpan.Zero), code.ValidFrom);
    }

    [Fact]
    public void TheRuleSet_FaithfullyReflectsTheAggregate()
    {
        var code = new DiscountCodeAggregate(Guid.NewGuid());
        code.Create(Definition("T-RULES", DiscountKind.FixedAmount, amount: 100.5m, scope: DiscountScope.Products, products: [Litter1]));
        code.Redeem();

        var rules = code.ToRuleSet();
        Assert.Equal(10_050L, rules.Amount!.Value.Value);
        Assert.Null(rules.PercentHundredths);
        Assert.Contains(Litter1, rules.ProductIds);
        Assert.Equal(1, rules.TimesRedeemed);
        Assert.Single(code.Events);
    }
}
