using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Ordering;

/// <summary>HO-ORD-001 §15 group D -- the pure checkout pricing and quote (US-ORD-02 to US-ORD-07, ADR-198, ADR-200).</summary>
public sealed class CheckoutPricingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 9); // a Friday
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid ZoneId = Guid.NewGuid();
    private static readonly Guid AddressId = Guid.NewGuid();

    private static OrderCatalogueSku Sku(string name, decimal price, bool prescription = false, bool purchasable = true, int category = 5, string currency = "BDT") =>
        new(Guid.NewGuid(), Guid.NewGuid(), name, name, null, category, ProductReturnClass.Other, price, currency, Guid.NewGuid(), purchasable, prescription, null, false, []);

    private static CheckoutLineInput Line(OrderCatalogueSku sku, int quantity, decimal? rate = 0m, decimal? addedAt = null, decimal sellable = 100m, Guid? catId = null,
        PrescriptionLineStatus rx = PrescriptionLineStatus.NotRequired) =>
        new(Guid.NewGuid(), sku, catId, quantity, addedAt ?? sku.CurrentPrice ?? 0m, sellable, rate, rx);

    private static DeliveryZoneFacts Zone(decimal baseCharge = 60m, decimal? freeOver = null, bool cod = true, decimal? codMax = null, params SlotWindow[] windows) =>
        new(ZoneId, "Dhaka city", BranchId, cod, codMax,
        [
            new ZoneServiceFacts(DeliveryServiceLevel.Standard, true, new ZoneChargeRules(baseCharge, freeOver, [new WeightBand(null, 0m)]), 1, windows),
            new ZoneServiceFacts(DeliveryServiceLevel.Express, true, new ZoneChargeRules(150m, null, []), 0, []),
        ]);

    private static CheckoutInputs Inputs(
        IReadOnlyList<CheckoutLineInput> lines, FulfilmentChoice? choice = null, DeliveryZoneFacts? zone = null, bool address = true, DiscountRuleSet? discount = null,
        string? discountText = null, CustomerDiscountFacts? facts = null, IReadOnlyList<PendingPolicyFact>? policies = null, PaymentSettingsFacts? payment = null,
        IReadOnlySet<DateOnly>? openDates = null, IReadOnlyList<PickupBranchFact>? pickup = null, DeliveryTaxFacts? deliveryTax = null) =>
        new(Now, Today, "BDT", lines,
            choice ?? new FulfilmentChoice(FulfilmentMethod.Delivery, AddressId, DeliveryServiceLevel.Standard, null, null, null, OrderPaymentMethod.CashOnDelivery, discountText),
            address ? new AddressFacts(AddressId, "House 1", null, "Dhaka", "Dhaka", "1212", null) : null,
            zone ?? Zone(), openDates ?? new HashSet<DateOnly>(), pickup ?? [new PickupBranchFact(BranchId, "Main", "1 Road", "0170")],
            discountText, discount, facts ?? new CustomerDiscountFacts(0, false), payment ?? new PaymentSettingsFacts(true, 5000m, true, true, true), policies ?? [], BranchId, deliveryTax ?? new DeliveryTaxFacts(Guid.Parse("00000000-0000-0000-0000-0000000000d1"), 0m));

    private static DiscountRuleSet Percent10Order() =>
        new(Guid.NewGuid(), "SAVE10", DiscountKind.Percentage, 1000, null, null, DiscountScope.Order, new HashSet<Guid>(), new HashSet<int>(), false, null, 0, null, null, null, true, "BDT");

    // ── FU1 D3 (owner decision 2026-10-10): the delivery charge carries the configured delivery tax, default 0 % ──
    [Fact]
    public void TheDeliveryTax_DefaultsToZeroPercent_SoTheTotalsAreUnchanged()
    {
        var lines = new[] { Line(Sku("Food", 850m), 2, rate: 15m) };

        var withoutSetting = CheckoutPricing.Quote(Inputs(lines));
        var explicitZero = CheckoutPricing.Quote(Inputs(lines, deliveryTax: new DeliveryTaxFacts(Guid.NewGuid(), 0m)));

        Assert.Equal(25_500L, withoutSetting.Totals.TaxTotal.Value);                       // lines only
        Assert.Equal(withoutSetting.Totals.TaxTotal, explicitZero.Totals.TaxTotal);
        Assert.Equal(withoutSetting.Totals.GrandTotal, explicitZero.Totals.GrandTotal);
    }

    [Fact]
    public void AConfiguredDeliveryTax_IsAddedToTheTaxTotalAndTheGrandTotal_RoundedHalfAwayFromZero()
    {
        var lines = new[] { Line(Sku("Food", 850m), 2, rate: 15m) };
        var before = CheckoutPricing.Quote(Inputs(lines));

        var quote = CheckoutPricing.Quote(Inputs(lines, deliveryTax: new DeliveryTaxFacts(Guid.NewGuid(), 15m)));

        Assert.Equal(6_000L, quote.Totals.DeliveryCharge.Value);                           // the charge itself is unchanged
        Assert.Equal(before.Totals.TaxTotal.Value + 900L, quote.Totals.TaxTotal.Value);    // 15 % of 60.00 = 9.00
        Assert.Equal(before.Totals.GrandTotal.Value + 900L, quote.Totals.GrandTotal.Value);
        Assert.NotEqual(before.Fingerprint, quote.Fingerprint);                            // the customer must see the change

        var odd = CheckoutPricing.Quote(Inputs(lines, zone: Zone(baseCharge: 50.10m), deliveryTax: new DeliveryTaxFacts(Guid.NewGuid(), 15m)));
        Assert.Equal(752L, odd.Totals.TaxTotal.Value - before.Totals.TaxTotal.Value);      // 15 % of 50.10 = 7.515 -> 7.52
    }

    // FU1 ORD-54: with no usable DELIVERY class a delivery quote cannot be placed (checked at quote time, not as a 5xx at placement); pickup and free delivery are unaffected.
    [Fact]
    public void ADeliveryChargeWithNoUsableDeliveryTaxClass_BlocksTheQuote_ButPickupDoesNot()
    {
        var lines = new[] { Line(Sku("Food", 850m), 1, rate: 15m) };
        var missing = new DeliveryTaxFacts(null, 0m);

        var quote = CheckoutPricing.Quote(Inputs(lines, deliveryTax: missing));
        Assert.False(quote.CanPlace);
        Assert.Contains(BusinessViolationCodes.DeliveryTaxNotConfigured, quote.BlockingReasons);

        var pickup = new FulfilmentChoice(FulfilmentMethod.Pickup, null, null, null, null, BranchId, OrderPaymentMethod.PayOnCollection, null);
        Assert.DoesNotContain(BusinessViolationCodes.DeliveryTaxNotConfigured, CheckoutPricing.Quote(Inputs(lines, choice: pickup, deliveryTax: missing)).BlockingReasons);
    }

    [Fact]
    public void ThePickupOrder_HasNoDeliveryCharge_SoTheDeliveryTaxAddsNothing()
    {
        var lines = new[] { Line(Sku("Food", 850m), 1, rate: 15m) };
        var pickup = new FulfilmentChoice(FulfilmentMethod.Pickup, null, null, null, null, BranchId, OrderPaymentMethod.PayOnCollection, null);

        var quote = CheckoutPricing.Quote(Inputs(lines, choice: pickup, deliveryTax: new DeliveryTaxFacts(Guid.NewGuid(), 15m)));

        Assert.Equal(0L, quote.Totals.DeliveryCharge.Value);
        Assert.Equal(12_750L, quote.Totals.TaxTotal.Value);
    }

    // ── owner decision 7 (2026-10-09): the zone's weight bands are applied to the cart's shipping weight ──
    [Fact]
    public void TheDeliveryCharge_AddsTheWeightBandSurcharge_ForTheCartsTotalShippingWeight()
    {
        var heavy = Sku("Litter 10 kg", 500m) with { ShippingWeightGrams = 10_000 };
        var light = Sku("Toy", 100m) with { ShippingWeightGrams = 200 };
        var zone = new DeliveryZoneFacts(ZoneId, "Dhaka city", BranchId, true, null,
        [
            new ZoneServiceFacts(DeliveryServiceLevel.Standard, true, new ZoneChargeRules(60m, null, [new WeightBand(2000, 0m), new WeightBand(5000, 40m), new WeightBand(null, 100m)]), 1, []),
        ]);

        decimal Charge(params CheckoutLineInput[] lines) => CheckoutPricing.Quote(Inputs(lines, zone: zone)).Totals.DeliveryCharge.ToAmount();

        Assert.Equal(60m, Charge(Line(light, 3)));                 // 600 g: first band, no surcharge
        Assert.Equal(100m, Charge(Line(light, 15)));               // 3,000 g: second band, +40
        Assert.Equal(160m, Charge(Line(heavy, 1)));                // 10,000 g: the open-ended band, +100
        Assert.Equal(160m, Charge(Line(light, 1), Line(heavy, 1))); // weights add up across lines
    }

    [Fact]
    public void ASkuWithoutAShippingWeight_CountsAsZeroGrams_SoNoSurchargeApplies()
    {
        var unknown = Sku("Litter 10 kg", 500m);
        var zone = new DeliveryZoneFacts(ZoneId, "Dhaka city", BranchId, true, null,
            [new ZoneServiceFacts(DeliveryServiceLevel.Standard, true, new ZoneChargeRules(60m, null, [new WeightBand(2000, 0m), new WeightBand(null, 100m)]), 1, [])]);

        Assert.Equal(60m, CheckoutPricing.Quote(Inputs([Line(unknown, 50)], zone: zone)).Totals.DeliveryCharge.ToAmount());
    }

    // ── U-D1 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D1_TheWorkedCart_HasEveryComponentSeparate_AndTheDocumentedTotal()
    {
        // 2 x 850.00 (VAT 15 %), 1 x 120.00 (VAT 0 %), a 10 % order code, delivery 60.00
        var a = Line(Sku("Food", 850m), 2, rate: 15m);
        var b = Line(Sku("Toy", 120m), 1, rate: 0m);
        var quote = CheckoutPricing.Quote(Inputs([a, b], discount: Percent10Order(), discountText: "save10"));

        Assert.Equal(182_000L, quote.Totals.Subtotal.Value);
        Assert.Equal(0L, quote.Totals.LineDiscountTotal.Value);
        Assert.Equal(18_200L, quote.Totals.OrderDiscount.Value);
        Assert.Equal(22_950L, quote.Totals.TaxTotal.Value);
        Assert.Equal(6_000L, quote.Totals.DeliveryCharge.Value);
        Assert.Equal(192_750L, quote.Totals.GrandTotal.Value); // 1927.50

        var food = quote.Lines.Single(l => l.LineRef == a.LineId);
        Assert.Equal(170_000L, food.Subtotal.Value);
        Assert.Equal(17_000L, food.OrderDiscountShare.Value);
        Assert.Equal(22_950L, food.TaxAmount.Value);
        Assert.Equal(175_950L, food.LineTotal.Value); // 1700 - 170 + 229.50

        var toy = quote.Lines.Single(l => l.LineRef == b.LineId);
        Assert.Equal(1_200L, toy.OrderDiscountShare.Value);
        Assert.Equal(0L, toy.TaxAmount.Value);

        Assert.True(quote.Discount!.Applied);
        Assert.Equal("SAVE10", quote.Discount.Code);
    }

    [Fact]
    public void U_D1_TheTotalAlwaysEqualsFR_ORD_5sFormula_OverManyRandomCarts()
    {
        var random = new Random(20261009);
        for (var run = 0; run < 300; run++)
        {
            var lines = Enumerable.Range(0, random.Next(1, 6))
                .Select(_ => Line(Sku("P" + random.Next(), random.Next(1, 500_000) / 100m), random.Next(1, 8), rate: random.Next(0, 3) * 7.5m))
                .ToList();
            var quote = CheckoutPricing.Quote(Inputs(lines, discount: Percent10Order(), discountText: "SAVE10"));
            var t = quote.Totals;

            Assert.Equal(t.Subtotal.Value - t.LineDiscountTotal.Value - t.OrderDiscount.Value + t.DeliveryCharge.Value + t.TaxTotal.Value, t.GrandTotal.Value);
            Assert.Equal(quote.Lines.Sum(l => l.OrderDiscountShare.Value), t.OrderDiscount.Value);   // the shares sum exactly (largest remainder)
            Assert.Equal(quote.Lines.Sum(l => l.TaxAmount.Value), t.TaxTotal.Value);                  // tax is rounded per line and summed, never rounded on the total
            Assert.All(quote.Lines, l => Assert.Equal(l.Subtotal.Value - l.LineDiscount.Value - l.OrderDiscountShare.Value + l.TaxAmount.Value, l.LineTotal.Value));
        }
    }

    [Fact]
    public void U_D1_Tax_IsRoundedHalfAwayFromZeroAtTheLine()
    {
        // 0.10 at 15 % = 0.015 -> 0.02 per line; two such lines are 0.04 although 15 % of 0.20 is 0.03
        var lines = new[] { Line(Sku("A", 0.10m), 1, rate: 15m), Line(Sku("B", 0.10m), 1, rate: 15m) };
        var quote = CheckoutPricing.Quote(Inputs(lines));

        Assert.All(quote.Lines, l => Assert.Equal(2L, l.TaxAmount.Value));
        Assert.Equal(4L, quote.Totals.TaxTotal.Value);
    }

    // ── U-D2 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D2_APriceRise_IsReported_WithBeforeAndAfter_AndPricedAtTheCurrentPrice()
    {
        var up = Line(Sku("Dearer", 130m), 1, addedAt: 100m);
        var down = Line(Sku("Cheaper", 80m), 1, addedAt: 100m);
        var quote = CheckoutPricing.Quote(Inputs([up, down]));

        var rise = quote.Changes.Single(c => c.Kind == CheckoutChangeKind.PriceIncreased);
        Assert.Equal(up.LineId, rise.LineRef);
        Assert.Equal("100.00", rise.Before);
        Assert.Equal("130.00", rise.After);
        Assert.Contains("increased", rise.Message);
        Assert.Equal(13_000L, quote.Lines.Single(l => l.LineRef == up.LineId).UnitPrice.Value);

        Assert.Equal(CheckoutChangeKind.PriceDecreased, quote.Changes.Single(c => c.LineRef == down.LineId).Kind);
    }

    [Fact]
    public void U_D2_StockOf1Against3_ReducesTheQuantity_AndNoStockExcludesTheLine()
    {
        var limited = Line(Sku("Limited", 100m), 3, sellable: 1);
        var gone = Line(Sku("Gone", 100m), 2, sellable: 0);
        var fractional = Line(Sku("Fractional", 100m), 2, sellable: 1.9m);
        var fine = Line(Sku("Fine", 100m), 1);
        var quote = CheckoutPricing.Quote(Inputs([limited, gone, fractional, fine]));

        Assert.Equal(1, quote.Lines.Single(l => l.LineRef == limited.LineId).Quantity);
        Assert.Equal(1, quote.Lines.Single(l => l.LineRef == fractional.LineId).Quantity); // a whole unit only
        var reduced = quote.Changes.First(c => c.Kind == CheckoutChangeKind.QuantityReduced && c.LineRef == limited.LineId);
        Assert.Equal("3", reduced.Before);
        Assert.Equal("1", reduced.After);

        var excluded = Assert.Single(quote.ExcludedLines);
        Assert.Equal(gone.LineId, excluded.LineRef);
        Assert.Equal(CheckoutChangeKind.NowUnavailable.DisplayName, excluded.ReasonCode);
        Assert.Contains(quote.Changes, c => c.Kind == CheckoutChangeKind.NowUnavailable && c.LineRef == gone.LineId);
        Assert.Equal(3, quote.Lines.Count);
    }

    [Fact]
    public void U_D2_AnArchivedProduct_OrADifferentCurrency_IsExcludedAsNowUnavailable()
    {
        var archived = Line(Sku("Archived", 100m, purchasable: false), 1);
        var foreign = Line(Sku("Foreign", 100m, currency: "USD"), 1);
        var fine = Line(Sku("Fine", 100m), 1);
        var quote = CheckoutPricing.Quote(Inputs([archived, foreign, fine]));

        Assert.Equal(2, quote.ExcludedLines.Count);
        Assert.Single(quote.Lines);
        Assert.True(quote.CanPlace);
    }

    [Fact]
    public void U_D2_ALapsedPrescriptionAuthorisation_ExcludesTheLine_WithABookingHint_ButDoesNotBlockTheRest()
    {
        var rx = Line(Sku("Renal diet", 900m, prescription: true), 1, catId: Guid.NewGuid(), rx: PrescriptionLineStatus.NotAuthorised);
        var other = Line(Sku("Litter", 100m), 1);
        var quote = CheckoutPricing.Quote(Inputs([rx, other]));

        Assert.Equal(CheckoutChangeKind.PrescriptionAuthorisationExpired.DisplayName, Assert.Single(quote.ExcludedLines).ReasonCode);
        var change = quote.Changes.Single(c => c.Kind == CheckoutChangeKind.PrescriptionAuthorisationExpired);
        Assert.Contains("consultation", change.Message);
        Assert.True(quote.CanPlace);
        Assert.Single(quote.Lines);
    }

    [Fact]
    public void U_D2_APrescriptionLineWithoutACat_MakesCanPlaceFalse_UntilTheCatIsSet()
    {
        var rx = Line(Sku("Renal diet", 900m, prescription: true), 1, rx: PrescriptionLineStatus.CatRequired);
        var other = Line(Sku("Litter", 100m), 1);

        var blocked = CheckoutPricing.Quote(Inputs([rx, other]));
        Assert.False(blocked.CanPlace);
        Assert.Contains("PrescriptionCatRequired", blocked.BlockingReasons);
        Assert.Contains(blocked.Changes, c => c.Kind == CheckoutChangeKind.PrescriptionCatRequired);

        var ready = CheckoutPricing.Quote(Inputs([Line(rx.Sku, 1, catId: Guid.NewGuid(), rx: PrescriptionLineStatus.Authorised), other]));
        Assert.True(ready.CanPlace);
        Assert.Equal(2, ready.Lines.Count);
        Assert.NotNull(ready.Lines.Single(l => l.SkuId == rx.Sku.SkuId).CatId);
    }

    [Fact]
    public void AnEmptyCart_AndACartWithNothingLeft_AreNotPlaceable()
    {
        Assert.Contains("CartEmpty", CheckoutPricing.Quote(Inputs([])).BlockingReasons);
        Assert.Equal(0L, CheckoutPricing.Quote(Inputs([])).Totals.GrandTotal.Value); // nothing to send, nothing to charge

        var onlyGone = CheckoutPricing.Quote(Inputs([Line(Sku("Gone", 100m), 1, sellable: 0)]));
        Assert.Contains("NothingToPlace", onlyGone.BlockingReasons);
        Assert.DoesNotContain("CartEmpty", onlyGone.BlockingReasons);
        Assert.False(onlyGone.CanPlace);
    }

    // ── U-D3 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D3_TheFingerprint_IsStable_AcrossRunsAndCultures()
    {
        var lines = new[] { Line(Sku("Food", 850.5m), 2, rate: 15m) };
        var inputs = Inputs(lines);

        var first = CheckoutPricing.Quote(inputs).Fingerprint;
        Assert.Equal(64, first.Length);
        Assert.Equal(first, CheckoutPricing.Quote(inputs).Fingerprint);

        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "bn-BD", "de-DE", "fr-FR", "tr-TR", "ar-SA" })
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(name);
                Assert.Equal(first, CheckoutPricing.Quote(inputs).Fingerprint);
            }
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void U_D3_TheFingerprint_ChangesWhenAnyPricedFieldTheMethodOrTheSlotChanges()
    {
        var sku = Sku("Food", 100m);
        var line = Line(sku, 2, rate: 15m);
        var baseline = CheckoutPricing.Quote(Inputs([line])).Fingerprint;
        var windows = new[] { new SlotWindow(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(13, 0)) };
        var open = new HashSet<DateOnly> { new(2026, 10, 12) };

        string Of(CheckoutInputs i) => CheckoutPricing.Quote(i).Fingerprint;

        // a priced field: quantity, price, tax rate
        Assert.NotEqual(baseline, Of(Inputs([Line(sku, 3, rate: 15m)])));
        Assert.NotEqual(baseline, Of(Inputs([Line(Sku("Food", 101m) with { SkuId = sku.SkuId, ProductId = sku.ProductId, TaxClassId = sku.TaxClassId }, 2, rate: 15m)])));
        Assert.NotEqual(baseline, Of(Inputs([Line(sku, 2, rate: 10m)])));

        // the method, the service level, the slot, the address, the pickup branch
        var choice = new FulfilmentChoice(FulfilmentMethod.Delivery, AddressId, DeliveryServiceLevel.Standard, null, null, null, OrderPaymentMethod.CashOnDelivery, null);
        Assert.NotEqual(baseline, Of(Inputs([line], choice with { PaymentMethod = OrderPaymentMethod.BankTransfer })));
        Assert.NotEqual(baseline, Of(Inputs([line], choice with { ServiceLevel = DeliveryServiceLevel.Express })));
        Assert.NotEqual(baseline, Of(Inputs([line], choice with { SlotDate = new DateOnly(2026, 10, 12), SlotWindowStart = new TimeOnly(10, 0) }, Zone(60m, null, true, null, windows), openDates: open)));
        Assert.NotEqual(baseline, Of(Inputs([line], choice with { AddressId = Guid.NewGuid() })));
        Assert.NotEqual(baseline, Of(Inputs([line], choice with { Method = FulfilmentMethod.Pickup, PickupBranchId = BranchId, PaymentMethod = OrderPaymentMethod.PayOnCollection })));

        // a discount that applies
        Assert.NotEqual(baseline, Of(Inputs([line], discount: Percent10Order(), discountText: "SAVE10")));
    }

    [Fact]
    public void U_D3_ADiscountThatDoesNotApply_DoesNotMoveTheFingerprint_ButIsReported()
    {
        var line = Line(Sku("Food", 100m), 1);
        var without = CheckoutPricing.Quote(Inputs([line]));
        var withBadCode = CheckoutPricing.Quote(Inputs([line], discount: null, discountText: "NOPE"));

        Assert.Equal(without.Fingerprint, withBadCode.Fingerprint);
        Assert.False(withBadCode.Discount!.Applied);
        Assert.Equal("DiscountCodeNotFound", withBadCode.Discount.ReasonCode);
        Assert.True(withBadCode.CanPlace); // the quote never fails for a bad code
    }

    // ── U-D5 (the pricing half) ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D5_ARateChangeBetweenQuoteAndPlacement_GivesANewFingerprint_SoPlacementRefuses()
    {
        var sku = Sku("Food", 1000m);
        var atQuote = CheckoutPricing.Quote(Inputs([Line(sku, 1, rate: 15m)]));
        var atPlacement = CheckoutPricing.Quote(Inputs([Line(sku, 1, rate: 17.5m)]));

        Assert.NotEqual(atQuote.Totals.TaxTotal.Value, atPlacement.Totals.TaxTotal.Value);
        Assert.NotEqual(atQuote.Fingerprint, atPlacement.Fingerprint);
    }

    // ── delivery (U-B1/U-B2/U-B3/U-B4 wired through the quote) ──────────────────────────────────────
    [Fact]
    public void AnAddressOutsideEveryZone_MakesDeliveryUnavailable_AndPickupIsOffered()
    {
        var quote = CheckoutPricing.Quote(Inputs([Line(Sku("A", 100m), 1)]) with { Zone = null });

        Assert.False(quote.Delivery.Available);
        Assert.Equal("AddressOutsideServedZones", quote.Delivery.UnavailableReason);
        Assert.False(quote.CanPlace);
        Assert.Contains("AddressOutsideServedZones", quote.BlockingReasons);
        Assert.NotEmpty(quote.PickupBranches);

        // the same cart as a pickup order is fine
        var pickup = CheckoutPricing.Quote((Inputs([Line(Sku("A", 100m), 1)]) with
        {
            Zone = null,
            Choice = new FulfilmentChoice(FulfilmentMethod.Pickup, null, null, null, null, BranchId, OrderPaymentMethod.PayOnCollection, null),
        }));
        Assert.True(pickup.CanPlace);
        Assert.Equal(0L, pickup.Totals.DeliveryCharge.Value);
    }

    [Fact]
    public void TheDeliveryCharge_IsTheChosenServiceLevels_AndFreeOverTheThreshold()
    {
        var line = Line(Sku("A", 1000m), 1);
        var standard = CheckoutPricing.Quote(Inputs([line], zone: Zone(freeOver: 2000m)));
        Assert.Equal(6_000L, standard.Totals.DeliveryCharge.Value);
        Assert.Equal(DeliveryServiceLevel.Standard, standard.SelectedServiceLevel);
        Assert.Equal(2, standard.Delivery.ServiceLevels.Count);

        var express = CheckoutPricing.Quote(Inputs([line], new FulfilmentChoice(FulfilmentMethod.Delivery, AddressId, DeliveryServiceLevel.Express, null, null, null, OrderPaymentMethod.CashOnDelivery, null)));
        Assert.Equal(15_000L, express.Totals.DeliveryCharge.Value);

        var free = CheckoutPricing.Quote(Inputs([Line(Sku("B", 2000m), 1)], zone: Zone(freeOver: 2000m)));
        Assert.Equal(0L, free.Totals.DeliveryCharge.Value);
        Assert.Equal(0L, free.Delivery.ServiceLevels.Single(l => l.Level == DeliveryServiceLevel.Standard).Charge.Value);
    }

    [Fact]
    public void TheFreeOverThreshold_IsJudgedOnGoodsAfterDiscounts()
    {
        // 2100 of goods, a 10 % code takes it to 1890 -- under the 2000 threshold, so delivery is charged
        var quote = CheckoutPricing.Quote(Inputs([Line(Sku("A", 2100m), 1)], zone: Zone(freeOver: 2000m), discount: Percent10Order(), discountText: "SAVE10"));
        Assert.Equal(6_000L, quote.Totals.DeliveryCharge.Value);
    }

    [Fact]
    public void ANoServiceLevelChoice_DefaultsToStandard_AndAnInactiveOrUnknownOneBlocks()
    {
        var line = Line(Sku("A", 100m), 1);
        var none = new FulfilmentChoice(FulfilmentMethod.Delivery, AddressId, null, null, null, null, OrderPaymentMethod.CashOnDelivery, null);
        Assert.Equal(DeliveryServiceLevel.Standard, CheckoutPricing.Quote(Inputs([line], none)).SelectedServiceLevel);

        var expressOnly = new DeliveryZoneFacts(ZoneId, "Dhaka", BranchId, true, null,
            [new ZoneServiceFacts(DeliveryServiceLevel.Express, true, new ZoneChargeRules(150m, null, []), 0, []), new ZoneServiceFacts(DeliveryServiceLevel.Standard, false, new ZoneChargeRules(60m, null, []), 1, [])]);
        var blocked = CheckoutPricing.Quote(Inputs([line], new FulfilmentChoice(FulfilmentMethod.Delivery, AddressId, DeliveryServiceLevel.Standard, null, null, null, OrderPaymentMethod.CashOnDelivery, null), expressOnly));
        Assert.Contains("ServiceLevelUnavailable", blocked.BlockingReasons);
        Assert.Equal(DeliveryServiceLevel.Express, CheckoutPricing.Quote(Inputs([line], none, expressOnly)).SelectedServiceLevel);
    }

    [Fact]
    public void ASlot_IsAcceptedWhenOffered_AndRefusedWhenNot_AndSlotsAreListedPerServiceLevel()
    {
        var windows = new[] { new SlotWindow(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(13, 0)) };
        var monday = new DateOnly(2026, 10, 12);
        var zone = Zone(60m, null, true, null, windows);
        var open = new HashSet<DateOnly> { monday, new(2026, 10, 19) };
        var line = Line(Sku("A", 100m), 1);

        FulfilmentChoice Choice(DateOnly? date, TimeOnly? start) =>
            new(FulfilmentMethod.Delivery, AddressId, DeliveryServiceLevel.Standard, date, start, null, OrderPaymentMethod.CashOnDelivery, null);

        var ok = CheckoutPricing.Quote(Inputs([line], Choice(monday, new TimeOnly(10, 0)), zone, openDates: open));
        Assert.True(ok.CanPlace);
        Assert.Equal(2, ok.Delivery.ServiceLevels.Single(l => l.Level == DeliveryServiceLevel.Standard).Slots.Count);
        Assert.Empty(ok.Delivery.ServiceLevels.Single(l => l.Level == DeliveryServiceLevel.Express).Slots);

        Assert.Contains("DeliverySlotUnavailable", CheckoutPricing.Quote(Inputs([line], Choice(monday, new TimeOnly(11, 0)), zone, openDates: open)).BlockingReasons);
        Assert.Contains("DeliverySlotUnavailable", CheckoutPricing.Quote(Inputs([line], Choice(monday.AddDays(1), new TimeOnly(10, 0)), zone, openDates: open)).BlockingReasons);
        Assert.Contains("DeliverySlotUnavailable", CheckoutPricing.Quote(Inputs([line], Choice(monday, null), zone, openDates: open)).BlockingReasons);
        Assert.Contains("DeliverySlotUnavailable", CheckoutPricing.Quote(Inputs([line], Choice(monday, new TimeOnly(10, 0)), zone, openDates: new HashSet<DateOnly>())).BlockingReasons); // branch closed
    }

    [Fact]
    public void ADeliveryOrder_NeedsAnAddress_AndAPickupOrder_NeedsAnOfferedBranch()
    {
        var line = Line(Sku("A", 100m), 1);

        Assert.Contains("DeliveryAddressNotFound", CheckoutPricing.Quote(Inputs([line], address: false)).BlockingReasons);

        var pickup = new FulfilmentChoice(FulfilmentMethod.Pickup, null, null, null, null, Guid.NewGuid(), OrderPaymentMethod.PayOnCollection, null);
        Assert.Contains("PickupBranchUnavailable", CheckoutPricing.Quote(Inputs([line], pickup)).BlockingReasons);
        Assert.Contains("PickupBranchUnavailable", CheckoutPricing.Quote(Inputs([line], pickup with { PickupBranchId = null })).BlockingReasons);
    }

    // ── payment options and policies ────────────────────────────────────────────────────────────────
    [Fact]
    public void ThePaymentOptions_AreListedWithTheirReasons_AndAnUnavailableChoiceBlocks()
    {
        var line = Line(Sku("Big", 6000m), 1);
        var quote = CheckoutPricing.Quote(Inputs([line]));

        var cod = quote.PaymentOptions.Single(o => o.Method == OrderPaymentMethod.CashOnDelivery);
        Assert.False(cod.Available);
        Assert.Equal("CodAboveOrderLimit", cod.UnavailableReason);
        Assert.Contains("PaymentMethodUnavailable", quote.BlockingReasons); // COD was the chosen method

        var transfer = CheckoutPricing.Quote(Inputs([line], new FulfilmentChoice(FulfilmentMethod.Delivery, AddressId, null, null, null, null, OrderPaymentMethod.BankTransfer, null)));
        Assert.True(transfer.CanPlace);

        var none = CheckoutPricing.Quote(Inputs([line], new FulfilmentChoice(FulfilmentMethod.Delivery, AddressId, null, null, null, null, null, null)));
        Assert.Contains("PaymentMethodUnavailable", none.BlockingReasons);
    }

    [Fact]
    public void APendingPolicy_BlocksPlacement_AndIsListed()
    {
        var quote = CheckoutPricing.Quote(Inputs([Line(Sku("A", 100m), 1)], policies: [new PendingPolicyFact("terms", 3, "Terms of sale")]));

        Assert.False(quote.CanPlace);
        Assert.Contains("PolicyAcceptanceRequired", quote.BlockingReasons);
        Assert.Equal("terms", Assert.Single(quote.PendingPolicies).Key);
    }

    [Fact]
    public void AScopedCode_GivesLineDiscounts_AndTaxIsOnTheDiscountedBase()
    {
        var litter = Line(Sku("Litter", 1000m, category: ProductCategory.Litter.Value), 1, rate: 15m);
        var toy = Line(Sku("Toy", 500m, category: ProductCategory.ToysAndFurniture.Value), 1, rate: 15m);
        var code = Percent10Order() with { Scope = DiscountScope.Categories, CategoryIds = new HashSet<int> { ProductCategory.Litter.Value } };

        var quote = CheckoutPricing.Quote(Inputs([litter, toy], discount: code, discountText: "SAVE10"));

        Assert.Equal(10_000L, quote.Totals.LineDiscountTotal.Value);
        Assert.Equal(0L, quote.Totals.OrderDiscount.Value);
        var litterLine = quote.Lines.Single(l => l.LineRef == litter.LineId);
        Assert.Equal(10_000L, litterLine.LineDiscount.Value);
        Assert.Equal(13_500L, litterLine.TaxAmount.Value);
        Assert.Equal(7_500L, quote.Lines.Single(l => l.LineRef == toy.LineId).TaxAmount.Value);
    }

    [Fact]
    public void NoMoneyAmountOfTheQuote_IsEverNegative()
    {
        var lines = new[] { Line(Sku("A", 10m), 1), Line(Sku("B", 0.01m), 3, rate: 15m) };
        var code = Percent10Order() with { Kind = DiscountKind.FixedAmount, PercentHundredths = null, Amount = new MinorUnits(99_999_999) };
        var quote = CheckoutPricing.Quote(Inputs(lines, discount: code, discountText: "BIG"));

        Assert.True(quote.Totals.GrandTotal.Value >= 0);
        Assert.All(quote.Lines, l => Assert.True(l.TaxAmount.Value >= 0 && l.LineTotal.Value >= 0));
        Assert.Equal(quote.Totals.Subtotal.Value, quote.Totals.OrderDiscount.Value); // capped at the goods
    }
}
