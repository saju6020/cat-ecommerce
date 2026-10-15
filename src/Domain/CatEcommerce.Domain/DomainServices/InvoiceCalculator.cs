using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One line as the calculator sees it. <see cref="Key"/> is the caller's own correlation id
/// (the invoice line id); <see cref="QualifiesForLoyalty"/> is false for a <c>Fee</c> (OQ-8).</summary>
public sealed record CalcLineInput(
    Guid Key, string CurrencyCode, decimal Quantity, decimal UnitPrice, decimal LineDiscount,
    decimal TaxRatePercent, bool QualifiesForLoyalty);

public sealed record CalcLineResult(
    Guid Key, decimal LineAmount, decimal NetBeforeShare, decimal InvoiceDiscountShare,
    decimal NetAmount, decimal TaxAmount, decimal GrossAmount);

public sealed record CalcTotals(
    IReadOnlyList<CalcLineResult> Lines, decimal NetTotal, decimal DiscountTotal, decimal TaxTotal, decimal Total);

/// <summary>What was already credited on one invoice line (summed from existing credit notes).</summary>
public sealed record CreditLineBasis(
    Guid InvoiceLineId, string Description, decimal Quantity, decimal NetAmount, decimal TaxAmount, decimal GrossAmount,
    decimal TaxRatePercent, decimal CreditedQuantity, decimal CreditedNet, decimal CreditedTax, decimal CreditedGross);

public sealed record CreditLineAmounts(decimal Net, decimal Tax, decimal Gross);

/// <summary>
/// HO-BIL-001 ADR-181 / ADR-194 -- <b>all</b> invoice and credit arithmetic, pure and explicit. Prices
/// are tax-exclusive (FR-ORD-5); tax per line is <c>round(net × rate / 100, 2, AwayFromZero)</c> and the
/// invoice tax is the sum of the line taxes, never the tax of the sum; an invoice-level (loyalty) discount
/// is apportioned pro-rata by net with the largest-remainder method, before tax; one currency only. Money
/// is <see cref="decimal"/> throughout -- a <c>float</c>/<c>double</c> never appears here (U-A7).
/// </summary>
public static class InvoiceCalculator
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal TaxOf(decimal net, decimal ratePercent) => Round(net * ratePercent / 100m);

    public static CalcTotals Calculate(IReadOnlyList<CalcLineInput> lines, decimal invoiceDiscount = 0m)
    {
        if (lines.Select(l => l.CurrencyCode).Distinct(StringComparer.Ordinal).Count() > 1)
            throw new DomainRuleViolationException(BusinessViolationCodes.CurrencyMismatch, "All lines of an invoice must be in one currency.", "CurrencyCode");

        var netBeforeShare = new Dictionary<Guid, decimal>();
        var lineAmounts = new Dictionary<Guid, decimal>();

        foreach (var line in lines)
        {
            if (line.Quantity <= 0m)
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidQuantity, "A quantity must be greater than zero.", "Quantity");

            var lineAmount = Round(line.Quantity * line.UnitPrice);
            if (line.LineDiscount < 0m || line.LineDiscount > lineAmount)
                throw new DomainRuleViolationException(BusinessViolationCodes.DiscountExceedsAmount, "A line discount cannot exceed the line amount.", "LineDiscount");

            lineAmounts[line.Key] = lineAmount;
            netBeforeShare[line.Key] = lineAmount - line.LineDiscount;
        }

        var qualifyingNet = lines.Where(l => l.QualifiesForLoyalty).Sum(l => netBeforeShare[l.Key]);
        if (invoiceDiscount < 0m || invoiceDiscount > qualifyingNet)
            throw new DomainRuleViolationException(BusinessViolationCodes.LoyaltyRedemptionExceedsNet, "The redemption cannot exceed the qualifying net of the invoice.", "Points");

        var shares = Apportion(invoiceDiscount, lines.Where(l => l.QualifiesForLoyalty).Select(l => (l.Key, netBeforeShare[l.Key])).ToList());

        var results = new List<CalcLineResult>();
        foreach (var line in lines)
        {
            var share = shares.GetValueOrDefault(line.Key);
            var net = netBeforeShare[line.Key] - share;
            var tax = TaxOf(net, line.TaxRatePercent);
            results.Add(new CalcLineResult(line.Key, lineAmounts[line.Key], netBeforeShare[line.Key], share, net, tax, net + tax));
        }

        var netTotal = results.Sum(r => r.NetAmount);
        var taxTotal = results.Sum(r => r.TaxAmount);
        var discountTotal = lines.Sum(l => l.LineDiscount) + results.Sum(r => r.InvoiceDiscountShare);
        return new CalcTotals(results, netTotal, discountTotal, taxTotal, netTotal + taxTotal);
    }

    /// <summary>Qualifying net (after line discounts, before the invoice-level discount and tax) of the
    /// lines that earn loyalty -- the cap for a redemption and the base for accrual (OQ-8).</summary>
    public static decimal QualifyingNetBeforeShare(IReadOnlyList<CalcLineInput> lines) =>
        lines.Where(l => l.QualifiesForLoyalty).Sum(l => Round(l.Quantity * l.UnitPrice) - l.LineDiscount);

    /// <summary>Largest-remainder apportionment of <paramref name="value"/> across weighted keys, in whole
    /// cents: floors first, the leftover cents to the largest fractional remainders (ties: the larger weight).</summary>
    public static IReadOnlyDictionary<Guid, decimal> Apportion(decimal value, IReadOnlyList<(Guid Key, decimal Weight)> weights)
    {
        var result = weights.ToDictionary(w => w.Key, _ => 0m);
        var totalWeight = weights.Sum(w => w.Weight);
        if (value <= 0m || totalWeight <= 0m) return result;

        var floors = new Dictionary<Guid, decimal>();
        var remainders = new List<(Guid Key, decimal Remainder, decimal Weight)>();
        foreach (var (key, weight) in weights)
        {
            var exactCents = value * 100m * weight / totalWeight;
            var floorCents = Math.Floor(exactCents);
            floors[key] = floorCents;
            remainders.Add((key, exactCents - floorCents, weight));
        }

        var leftoverCents = (int)Math.Round(value * 100m - floors.Values.Sum(), 0, MidpointRounding.AwayFromZero);
        foreach (var item in remainders.OrderByDescending(r => r.Remainder).ThenByDescending(r => r.Weight).Take(leftoverCents))
            floors[item.Key] += 1m;

        foreach (var key in floors.Keys) result[key] = floors[key] / 100m;
        return result;
    }

    /// <summary>Credit of a quantity of an invoice line at the line's <b>frozen</b> rate (US-BIL-05, U-A8).
    /// Crediting everything that remains uses the line's own remaining net and tax so totals tie out.</summary>
    public static CreditLineAmounts CreditByQuantity(CreditLineBasis basis, decimal quantity)
    {
        if (quantity <= 0m)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidQuantity, "A quantity must be greater than zero.", "Quantity");

        if (basis.CreditedQuantity + quantity > basis.Quantity)
            throw ExceedsLine(basis);

        if (basis.CreditedQuantity + quantity == basis.Quantity)
            return Remainder(basis);

        var net = Round(basis.NetAmount * quantity / basis.Quantity);
        var tax = TaxOf(net, basis.TaxRatePercent);
        return CapToLine(basis, new CreditLineAmounts(net, tax, net + tax));
    }

    /// <summary>Credit of a gross (tax-inclusive) amount of a line; the net and tax are split at the frozen rate.</summary>
    public static CreditLineAmounts CreditByAmount(CreditLineBasis basis, decimal grossAmount)
    {
        if (grossAmount <= 0m)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "A credit amount must be greater than zero.", "Amount");

        var remainingGross = basis.GrossAmount - basis.CreditedGross;
        if (grossAmount > remainingGross) throw ExceedsLine(basis);
        if (grossAmount == remainingGross) return Remainder(basis);

        var net = Round(grossAmount / (1m + basis.TaxRatePercent / 100m));
        return CapToLine(basis, new CreditLineAmounts(net, grossAmount - net, grossAmount));
    }

    /// <summary>Everything that has not been credited yet on the line ("full credit").</summary>
    public static CreditLineAmounts Remainder(CreditLineBasis basis) =>
        new(basis.NetAmount - basis.CreditedNet, basis.TaxAmount - basis.CreditedTax, basis.GrossAmount - basis.CreditedGross);

    private static CreditLineAmounts CapToLine(CreditLineBasis basis, CreditLineAmounts amounts)
    {
        if (basis.CreditedGross + amounts.Gross > basis.GrossAmount ||
            basis.CreditedNet + amounts.Net > basis.NetAmount ||
            basis.CreditedTax + amounts.Tax > basis.TaxAmount)
            throw ExceedsLine(basis);

        return amounts;
    }

    private static DomainRuleViolationException ExceedsLine(CreditLineBasis basis) =>
        new(BusinessViolationCodes.CreditExceedsLine, BillingMessages.CreditExceedsLine(basis.Description), "Lines");
}
