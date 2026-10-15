using System.Globalization;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.15 (ADR-207), US-ORD-05 -- pure. One ordered rule list; the <b>first failing rule</b> is the answer, each with its own code and
/// a plain message (AC2): not found, inactive, not yet valid, expired, usage limit reached, customer limit reached, first-order-only, not
/// applicable to the items, minimum spend not met. When every rule passes, the amounts are computed in minor units (ADR-198): a <b>scoped</b> code
/// gives each matching line a line discount (a percentage of the line, or a share of the fixed value apportioned across matching lines by subtotal,
/// largest remainder so the shares sum exactly; never more than the line); an <b>unscoped</b> code gives one order discount over
/// <c>Σ(subtotal - lineDiscount)</c> apportioned across lines by the same method into <c>OrderDiscountShare</c>, which the tax base and the refund
/// base need. One code per order.
/// </summary>
public static class DiscountRules
{
    public static DiscountEvaluation Evaluate(
        DiscountRuleSet? code, IReadOnlyList<DiscountCartLine> lines, CustomerDiscountFacts customer, DateTimeOffset now)
    {
        if (code is null)
            return DiscountEvaluation.Failure(BusinessViolationCodes.DiscountCodeNotFound, "We could not find that discount code. Check the spelling and try again.");

        if (!code.IsActive)
            return DiscountEvaluation.Failure(BusinessViolationCodes.DiscountCodeInactive, "This discount code is not active.");

        if (code.ValidFrom is { } from && now < from)
            return DiscountEvaluation.Failure(BusinessViolationCodes.DiscountCodeNotYetValid, $"This discount code is valid from {from.UtcDateTime:yyyy-MM-dd}.");

        if (code.ValidTo is { } to && now > to)
            return DiscountEvaluation.Failure(BusinessViolationCodes.DiscountCodeExpired, "This discount code has expired.");

        if (code.UsageLimit is { } limit && code.TimesRedeemed >= limit)
            return DiscountEvaluation.Failure(BusinessViolationCodes.DiscountUsageLimitReached, "This discount code has been used the maximum number of times.");

        if (code.PerCustomerLimit is { } perCustomer && customer.TimesUsedByCustomer >= perCustomer)
            return DiscountEvaluation.Failure(BusinessViolationCodes.DiscountCustomerLimitReached, "You have already used this discount code the maximum number of times.");

        if (code.FirstOrderOnly && customer.HasPriorNonCancelledOrder)
            return DiscountEvaluation.Failure(BusinessViolationCodes.DiscountFirstOrderOnly, "This code is for a first order only.");

        var eligible = EligibleLines(code, lines);
        if (eligible.Count == 0)
            return DiscountEvaluation.Failure(BusinessViolationCodes.DiscountNotApplicableToItems, "This code does not apply to any item in your cart.");

        var eligibleSubtotal = new MinorUnits(eligible.Sum(l => l.Subtotal.Value));
        if (code.MinimumSpend is { } minimum && eligibleSubtotal < minimum)
            return DiscountEvaluation.Failure(BusinessViolationCodes.DiscountMinimumSpendNotMet,
                $"Spend {code.CurrencyCode} {minimum.ToAmount().ToString("N2", CultureInfo.InvariantCulture)} on eligible items to use this code.");

        return DiscountEvaluation.Success(code.Scope == DiscountScope.Order ? ApplyToOrder(code, lines) : ApplyToLines(code, eligible, eligibleSubtotal));
    }

    /// <summary>The lines a code can discount: every line for an order-scoped code, the matching products or categories otherwise.</summary>
    public static IReadOnlyList<DiscountCartLine> EligibleLines(DiscountRuleSet code, IReadOnlyList<DiscountCartLine> lines) =>
        code.Scope == DiscountScope.Products ? lines.Where(l => code.ProductIds.Contains(l.ProductId)).ToList()
        : code.Scope == DiscountScope.Categories ? lines.Where(l => code.CategoryIds.Contains(l.CategoryId)).ToList()
        : lines.ToList();

    /// <summary>A percentage of <paramref name="basis"/>, or a fixed value capped at it (a code never discounts more than the goods).</summary>
    private static MinorUnits Value(DiscountRuleSet code, MinorUnits basis) =>
        code.Kind == DiscountKind.Percentage
            ? basis.Percent(code.PercentHundredths!.Value)
            : MinorUnits.Min(code.Amount!.Value, basis);

    private static DiscountApplication ApplyToLines(DiscountRuleSet code, IReadOnlyList<DiscountCartLine> eligible, MinorUnits eligibleSubtotal)
    {
        var lineDiscounts = new Dictionary<Guid, MinorUnits>();

        if (code.Kind == DiscountKind.Percentage)
        {
            foreach (var line in eligible)
                lineDiscounts[line.LineRef] = MinorUnits.Min(line.Subtotal.Percent(code.PercentHundredths!.Value), line.Subtotal);
        }
        else
        {
            var shares = MinorUnits.Apportion(Value(code, eligibleSubtotal), eligible.Select(l => l.Subtotal).ToList());
            for (var i = 0; i < eligible.Count; i++)
                lineDiscounts[eligible[i].LineRef] = MinorUnits.Min(shares[i], eligible[i].Subtotal);
        }

        return new DiscountApplication(lineDiscounts, MinorUnits.Zero, new Dictionary<Guid, MinorUnits>());
    }

    private static DiscountApplication ApplyToOrder(DiscountRuleSet code, IReadOnlyList<DiscountCartLine> lines)
    {
        var basis = new MinorUnits(lines.Sum(l => l.Subtotal.Value)); // no line discount exists when the code is unscoped
        var orderDiscount = Value(code, basis);

        var shares = MinorUnits.Apportion(orderDiscount, lines.Select(l => l.Subtotal).ToList());
        var byLine = new Dictionary<Guid, MinorUnits>();
        for (var i = 0; i < lines.Count; i++) byLine[lines[i].LineRef] = shares[i];

        return new DiscountApplication(new Dictionary<Guid, MinorUnits>(), orderDiscount, byLine);
    }
}
