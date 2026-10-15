using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>A priced quote: the units in chronological order, the basis they were priced on, and the totals.</summary>
public sealed record BoardingQuote(StayRateBasis Basis, IReadOnlyList<QuotedRateUnit> Units, decimal Gross, decimal Discount, decimal Total)
{
    public static BoardingQuote Empty(StayRateBasis basis) => new(basis, [], 0m, 0m, 0m);
}

/// <summary>
/// HO-BRD-001 section 8.8 (ADR-221, renumbered ADR-354) -- the ONE pricing function behind availability, reserve, extension, early check-out and the
/// per-cycle invoices. Pure.
///
/// <list type="bullet">
/// <item>basis: fewer nights than the long-stay threshold price nightly; from the threshold up to 29 nights weekly; 30 and over monthly.</item>
/// <item>units, from night 1: monthly basis is whole months of 30 nights then the remainder as weeks; weekly basis is whole weeks then nights; nightly
/// basis is nights. Consecutive nights of one kind are ONE unit with a quantity (the handover's "5 night units" are one Night unit covering 5 nights:
/// the stay keeps at most 40 units, section 8.1) -- a month never straddles a 30-night billing cycle, so a unit belongs to exactly one cycle.</item>
/// <item>every cat beyond the first adds an AdditionalCat unit alongside each rate unit: (cats - 1) x rate x the unit's nights.</item>
/// <item>when the basis is not nightly and the discount is above 0, EACH unit carries its own discount, rounded away from zero to the paisa -- so the sum
/// of per-cycle invoices equals the quote exactly (U-B8).</item>
/// </list>
/// </summary>
public static class BoardingQuoteCalculator
{
    public static StayRateBasis BasisFor(int nights, int longStayThresholdNights)
    {
        if (nights < longStayThresholdNights) return StayRateBasis.Nightly;
        return nights < StayRules.NightsPerMonthUnit ? StayRateBasis.Weekly : StayRateBasis.Monthly;
    }

    public static BoardingQuote Quote(int nights, int catCount, RateCardSnapshot card, PricingSnapshot pricing) =>
        Quote(nights, catCount, card, pricing, Guid.NewGuid);

    /// <summary><paramref name="newId"/> mints unit ids -- fixed in tests, <see cref="Guid.NewGuid"/> in production.</summary>
    public static BoardingQuote Quote(int nights, int catCount, RateCardSnapshot card, PricingSnapshot pricing, Func<Guid> newId)
    {
        if (nights <= 0) return BoardingQuote.Empty(StayRateBasis.Nightly);

        var basis = BasisFor(nights, pricing.LongStayThresholdNights);
        var type = card.UnitType.DisplayName;
        var units = new List<QuotedRateUnit>();
        var sequence = 0;
        var night = 1;
        var remaining = nights;

        void AddRange(RateUnitKind kind, int count, string label)
        {
            var from = night;
            var to = night + count - 1;
            var rate = kind == RateUnitKind.Month ? card.MonthlyRate : kind == RateUnitKind.Week ? card.WeeklyRate : card.NightlyRate;
            // A month or a week is ONE priced unit; nights are priced one by one and grouped.
            var amount = kind == RateUnitKind.Night ? rate * count : rate;

            units.Add(NewUnit(newId(), ++sequence, kind, from, to, amount, basis, pricing.LongStayDiscountPercent, label));
            if (catCount > 1 && card.AdditionalCatNightlyRate > 0m)
            {
                var extra = (catCount - 1) * card.AdditionalCatNightlyRate * count;
                units.Add(NewUnit(newId(), ++sequence, RateUnitKind.AdditionalCat, from, to, extra, basis, pricing.LongStayDiscountPercent,
                    $"Additional cats x{catCount - 1} ({Range(from, to)}) - {type} additional-cat rate"));
            }

            night += count;
            remaining -= count;
        }

        string Range(int from, int to) => from == to ? $"night {from}" : $"nights {from}-{to}";

        if (basis == StayRateBasis.Monthly)
        {
            for (var i = 1; remaining >= StayRules.NightsPerMonthUnit; i++)
                AddRange(RateUnitKind.Month, StayRules.NightsPerMonthUnit, $"Month {i} ({Range(night, night + StayRules.NightsPerMonthUnit - 1)}) - {type} monthly rate");
        }

        if (basis != StayRateBasis.Nightly)
        {
            for (var i = 1; remaining >= StayRules.NightsPerWeekUnit; i++)
                AddRange(RateUnitKind.Week, StayRules.NightsPerWeekUnit, $"Week {i} ({Range(night, night + StayRules.NightsPerWeekUnit - 1)}) - {type} weekly rate");
        }

        if (remaining > 0)
            AddRange(RateUnitKind.Night, remaining, $"{Capital(Range(night, night + remaining - 1))} - {type} nightly rate");

        var gross = units.Sum(u => u.Amount);
        var discount = units.Sum(u => u.Discount);
        return new BoardingQuote(basis, units, gross, discount, gross - discount);
    }

    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private static QuotedRateUnit NewUnit(Guid id, int sequence, RateUnitKind kind, int from, int to, decimal amount, StayRateBasis basis, decimal discountPercent, string description)
    {
        var discount = basis != StayRateBasis.Nightly && discountPercent > 0m
            ? decimal.Round(amount * discountPercent / 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return new QuotedRateUnit(id, sequence, kind, from, to, decimal.Round(amount, 2, MidpointRounding.AwayFromZero), discount, description);
    }

    /// <summary>The units (and so the charges) whose first night falls in billing cycle <paramref name="cycle"/> (1-based, 30 nights each).</summary>
    public static IEnumerable<QuotedRateUnit> UnitsInCycle(IEnumerable<QuotedRateUnit> units, int cycle) =>
        units.Where(u => (u.FromNight - 1) / StayRules.NightsPerCycle + 1 == cycle);
}
