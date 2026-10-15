using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.5 (ADR-198, ADR-200), US-ORD-02 to US-ORD-07 -- the checkout quote, a <b>pure</b> function of <see cref="CheckoutInputs"/>: no clock, no
/// I/O. All arithmetic is on <c>long</c> minor units; tax is rounded half away from zero <b>at the line</b>, never on the total. In order:
/// <list type="number">
/// <item>Lines: each cart line is priced at the price in force and capped at what can be sold; a line that cannot be bought is excluded with a change.</item>
/// <item>The discount code (the first failing rule is reported, never thrown): line discounts for a scoped code, an order discount apportioned into
/// per-line shares for an unscoped one.</item>
/// <item>Tax per line on <c>subtotal - lineDiscount - orderDiscountShare</c> at the rate in force.</item>
/// <item>The delivery charge for the chosen service level (taxed at the configured delivery rate, default 0 %: FU1 D3), the slots on offer, and the payment options with their reasons.</item>
/// <item><c>GrandTotal = Σsubtotal - ΣlineDiscount - orderDiscount + delivery + Σtax</c> -- FR-ORD-5's formula, each component its own field.</item>
/// <item>The fingerprint: SHA-256 of a canonical, culture-invariant serialisation of every choice and every priced amount. It is a comparison token, not a
/// secret; placement recomputes the quote and refuses unless the customer's fingerprint matches (<c>409 CheckoutQuoteChanged</c>).</item>
/// </list>
/// </summary>
public static class CheckoutPricing
{
    public static CheckoutQuote Quote(CheckoutInputs i)
    {
        var changes = new List<CheckoutChange>();
        var excluded = new List<ExcludedLine>();
        var priced = new List<PricedLine>();
        var blocking = new List<string>();

        foreach (var line in i.Lines) Classify(i, line, priced, excluded, changes);

        // A prescription line without a cat is the reason to show first (I-D5): "nothing to place" would hide the one thing the customer can fix.
        var catRequired = i.Lines.Any(l => l.Prescription == PrescriptionLineStatus.CatRequired);
        if (i.Lines.Count == 0) blocking.Add(BusinessViolationCodes.CartEmpty);
        else if (priced.Count == 0 && !catRequired) blocking.Add(BusinessViolationCodes.NothingToPlace);
        if (catRequired) blocking.Add(BusinessViolationCodes.PrescriptionCatRequired);

        // ── discount ──
        var discountLines = priced.Select(p => new DiscountCartLine(p.Line.LineId, p.Line.Sku.ProductId, p.Line.Sku.CategoryId, p.Subtotal)).ToList();
        QuoteDiscount? discountView = null;
        var application = DiscountApplication.None;
        if (!string.IsNullOrWhiteSpace(i.DiscountCodeText))
        {
            var evaluation = DiscountRules.Evaluate(i.Discount, discountLines, i.CustomerFacts, i.Now);
            application = evaluation.Application;
            discountView = new QuoteDiscount(i.DiscountCodeText.Trim().ToUpperInvariant(), evaluation.Applied, evaluation.ReasonCode, evaluation.Message);
        }

        // ── lines with discount shares and tax ──
        var quoteLines = priced.Select(p =>
        {
            var lineDiscount = application.LineDiscounts.GetValueOrDefault(p.Line.LineId);
            var share = application.OrderDiscountShares.GetValueOrDefault(p.Line.LineId);
            var taxBase = MinorUnits.Max(p.Subtotal - lineDiscount - share, MinorUnits.Zero);
            var tax = taxBase.Percent(MinorUnits.PercentToHundredths(p.Line.TaxRatePercent!.Value));

            return new QuoteLine(
                p.Line.LineId, p.Line.Sku.SkuId, p.Line.Sku.ProductId, p.Line.Sku.ProductName, p.Line.Sku.VariantLabel, p.Line.Sku.Barcode, p.Line.Sku.ReturnClass,
                p.Line.Sku.IsBundle, p.Line.Sku.RequiresPrescription ? p.Line.CatId : null, p.Quantity, p.Unit, p.Subtotal, lineDiscount, share, p.Line.Sku.TaxClassId,
                p.Line.TaxRatePercent!.Value, tax, taxBase + tax, p.Line.Sku.BundleComponents);
        }).ToList();

        var subtotal = Sum(quoteLines, l => l.Subtotal);
        var lineDiscountTotal = Sum(quoteLines, l => l.LineDiscount);
        var orderDiscount = application.OrderDiscount;
        var taxTotal = Sum(quoteLines, l => l.TaxAmount);
        var goodsValue = subtotal - lineDiscountTotal - orderDiscount;

        // ── delivery / pickup ──
        var (delivery, selectedLevel, resolvedCharge) = ResolveDelivery(i, goodsValue, priced.Sum(p => (long)(p.Line.Sku.ShippingWeightGrams ?? 0) * p.Quantity), blocking);
        var deliveryCharge = priced.Count == 0 ? MinorUnits.Zero : resolvedCharge; // nothing to send, nothing to charge
        ValidatePickup(i, blocking);

        // FU1 D3: the delivery charge carries the configured delivery tax (default 0 %), rounded half away from zero like every line.
        // FU1 ORD-54: a delivery charge needs the DELIVERY class; if it is unusable the quote cannot be placed, so nobody gets a valid quote and then a 5xx.
        if (i.Choice.Method == FulfilmentMethod.Delivery && deliveryCharge.Value > 0 && i.DeliveryTax?.TaxClassId is null)
            blocking.Add(BusinessViolationCodes.DeliveryTaxNotConfigured);

        var deliveryTax = deliveryCharge.Percent(MinorUnits.PercentToHundredths(i.DeliveryTax?.RatePercent ?? 0m));
        taxTotal += deliveryTax;
        var totals = new QuoteTotals(subtotal, lineDiscountTotal, orderDiscount, deliveryCharge, taxTotal, subtotal - lineDiscountTotal - orderDiscount + deliveryCharge + taxTotal);

        // ── payment ──
        // A counter sale is paid by tenders at the till (TenderCalculator), not by an online option, and needs no policy acceptance: neither blocks it.
        var isCounter = i.Choice.Method == FulfilmentMethod.Counter;
        IReadOnlyList<PaymentOption> options = isCounter ? [] : PaymentOptionPolicy.Evaluate(new PaymentOptionInputs(
            i.Choice.Method, i.Payment.CodEnabled, i.Payment.CodMaxOrderValue,
            i.Choice.Method == FulfilmentMethod.Delivery ? i.Zone?.CodEnabled : null, i.Zone?.CodMaxOrderValue,
            totals.GrandTotal, i.Payment.BankTransferEnabled, i.Payment.BankAccountActive, i.Payment.PayOnCollectionEnabled, i.CurrencyCode));

        if (!isCounter && (i.Choice.PaymentMethod is not { } chosen || !options.Any(o => o.Method == chosen && o.Available)))
            blocking.Add(BusinessViolationCodes.PaymentMethodUnavailable);

        if (!isCounter && i.PendingPolicies.Count > 0) blocking.Add(BusinessViolationCodes.PolicyAcceptanceRequired);

        var distinctBlocking = blocking.Distinct().ToList();
        var fingerprint = Fingerprint(i, delivery, selectedLevel, discountView, quoteLines, totals);

        return new CheckoutQuote(
            quoteLines, excluded, changes, delivery, selectedLevel, i.PickupBranches, options, discountView, totals, i.PendingPolicies,
            distinctBlocking.Count == 0, distinctBlocking, fingerprint, i.CurrencyCode, i.FulfilmentBranchId);
    }

    private sealed record PricedLine(CheckoutLineInput Line, int Quantity, MinorUnits Unit, MinorUnits Subtotal);

    private static MinorUnits Sum(IEnumerable<QuoteLine> lines, Func<QuoteLine, MinorUnits> pick) => new(lines.Sum(l => pick(l).Value));

    // ── step 1: lines ───────────────────────────────────────────────────────────────────────────────
    private static void Classify(CheckoutInputs i, CheckoutLineInput line, List<PricedLine> priced, List<ExcludedLine> excluded, List<CheckoutChange> changes)
    {
        var sku = line.Sku;
        var name = sku.ProductName;

        void Exclude(CheckoutChangeKind kind, string reason, string message, string? before = null, string? after = null)
        {
            excluded.Add(new ExcludedLine(line.LineId, sku.SkuId, name, line.CatId, line.CartQuantity, reason));
            changes.Add(new CheckoutChange(kind, line.LineId, name, before, after, message));
        }

        // A taxable product whose tax class has no rate in force cannot be priced: it is left out rather than sold untaxed (Billing refuses the same, TaxRateNotConfigured).
        if (line.TaxRatePercent is null)
        {
            Exclude(CheckoutChangeKind.NowUnavailable, "TaxRateNotConfigured", $"'{name}' cannot be priced right now and has been left out of this order.");
            return;
        }

        if (!sku.IsPurchasable || sku.CurrentPrice is null || !string.Equals(sku.CurrencyCode, i.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            Exclude(CheckoutChangeKind.NowUnavailable, nameof(CheckoutChangeKind.NowUnavailable), $"'{name}' is no longer available and has been left out of this order.");
            return;
        }

        if (sku.RequiresPrescription && line.Prescription == PrescriptionLineStatus.CatRequired)
        {
            excluded.Add(new ExcludedLine(line.LineId, sku.SkuId, name, line.CatId, line.CartQuantity, nameof(CheckoutChangeKind.PrescriptionCatRequired)));
            changes.Add(new CheckoutChange(CheckoutChangeKind.PrescriptionCatRequired, line.LineId, name, null, null, $"Choose which cat '{name}' is for before you check out."));
            return;
        }

        if (sku.RequiresPrescription && line.Prescription == PrescriptionLineStatus.NotAuthorised)
        {
            Exclude(CheckoutChangeKind.PrescriptionAuthorisationExpired, nameof(CheckoutChangeKind.PrescriptionAuthorisationExpired),
                $"'{name}' needs a current veterinary authorisation for your cat, and it is no longer valid. Book a consultation to renew it.");
            return;
        }

        var sellable = (int)Math.Floor(Math.Min(line.Sellable, int.MaxValue));
        if (sellable <= 0)
        {
            Exclude(CheckoutChangeKind.NowUnavailable, nameof(CheckoutChangeKind.NowUnavailable), $"'{name}' has just sold out and has been left out of this order.");
            return;
        }

        var quantity = Math.Min(line.CartQuantity, sellable);
        if (quantity < line.CartQuantity)
            changes.Add(new CheckoutChange(CheckoutChangeKind.QuantityReduced, line.LineId, name, line.CartQuantity.ToString(CultureInfo.InvariantCulture),
                quantity.ToString(CultureInfo.InvariantCulture), $"Only {quantity} of '{name}' {(quantity == 1 ? "is" : "are")} left. The quantity has been reduced."));

        var unit = MinorUnits.FromAmount(sku.CurrentPrice.Value);
        var before = MinorUnits.FromAmount(line.PriceWhenAdded);
        if (unit != before)
        {
            var kind = unit > before ? CheckoutChangeKind.PriceIncreased : CheckoutChangeKind.PriceDecreased;
            changes.Add(new CheckoutChange(kind, line.LineId, name, before.ToString(), unit.ToString(),
                $"The price of '{name}' has {(unit > before ? "increased" : "decreased")} from {i.CurrencyCode} {before} to {i.CurrencyCode} {unit}."));
        }

        priced.Add(new PricedLine(line, quantity, unit, unit.Times(quantity)));
    }

    // ── step 4: delivery ────────────────────────────────────────────────────────────────────────────
    private static (QuoteDelivery Delivery, DeliveryServiceLevel? Selected, MinorUnits Charge) ResolveDelivery(CheckoutInputs i, MinorUnits goodsValue, long totalWeightGrams, List<string> blocking)
    {
        var isDelivery = i.Choice.Method == FulfilmentMethod.Delivery;

        if (i.Address is null)
        {
            if (isDelivery) blocking.Add(BusinessViolationCodes.DeliveryAddressNotFound);
            return (new QuoteDelivery(false, null, null, null, null, []), null, MinorUnits.Zero);
        }

        if (i.Zone is not { } zone)
        {
            if (isDelivery) blocking.Add(BusinessViolationCodes.AddressOutsideServedZones);
            return (new QuoteDelivery(false, BusinessViolationCodes.AddressOutsideServedZones, null, null, null, []), null, MinorUnits.Zero);
        }

        var levels = zone.Services.Where(s => s.IsActive).OrderBy(s => s.Level.Value).Select(s => new QuoteServiceLevel(
            s.Level, DeliveryChargeCalculator.Calculate(s.Charge, goodsValue, totalWeightGrams), s.LeadTimeDays,
            DeliverySlotCalculator.Slots(s.SlotWindows, s.LeadTimeDays, i.TodayLocal, i.OpenDates.Contains))).ToList();

        var delivery = new QuoteDelivery(true, null, zone.ZoneId, zone.Name, zone.BranchId, levels);
        if (!isDelivery) return (delivery, null, MinorUnits.Zero);

        // The chosen service level; with none chosen, Standard if offered, else the first on offer.
        QuoteServiceLevel? selected = i.Choice.ServiceLevel is { } wanted
            ? levels.FirstOrDefault(l => l.Level == wanted)
            : levels.FirstOrDefault(l => l.Level == DeliveryServiceLevel.Standard) ?? levels.FirstOrDefault();

        if (selected is null)
        {
            blocking.Add(BusinessViolationCodes.ServiceLevelUnavailable);
            return (delivery, null, MinorUnits.Zero);
        }

        if (i.Choice.SlotDate is { } date)
        {
            var offered = i.Choice.SlotWindowStart is { } start && selected.Slots.Any(s => s.Date == date && s.WindowStart == start);
            if (!offered) blocking.Add(BusinessViolationCodes.DeliverySlotUnavailable);
        }

        return (delivery, selected.Level, selected.Charge);
    }

    private static void ValidatePickup(CheckoutInputs i, List<string> blocking)
    {
        if (i.Choice.Method != FulfilmentMethod.Pickup) return;

        if (i.Choice.PickupBranchId is not { } branchId || i.PickupBranches.All(b => b.BranchId != branchId))
            blocking.Add(BusinessViolationCodes.PickupBranchUnavailable);
    }

    // ── step 7: the fingerprint ─────────────────────────────────────────────────────────────────────
    private static string Fingerprint(
        CheckoutInputs i, QuoteDelivery delivery, DeliveryServiceLevel? level, QuoteDiscount? discount, IReadOnlyList<QuoteLine> lines, QuoteTotals totals)
    {
        var inv = CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        text.Append("v1|").Append(i.Choice.Method.Value.ToString(inv))
            .Append('|').Append(i.Choice.AddressId?.ToString("D"))
            .Append('|').Append(i.Choice.Method == FulfilmentMethod.Delivery ? delivery.ZoneId?.ToString("D") : null)
            .Append('|').Append(level?.Value.ToString(inv))
            .Append('|').Append(i.Choice.SlotDate?.ToString("yyyy-MM-dd", inv))
            .Append('|').Append(i.Choice.SlotWindowStart?.ToString("HH:mm", inv))
            .Append('|').Append(i.Choice.PickupBranchId?.ToString("D"))
            .Append('|').Append(i.Choice.PaymentMethod?.Value.ToString(inv))
            .Append('|').Append(discount is { Applied: true } ? discount.Code : string.Empty)
            .Append('|').Append(i.CurrencyCode.ToUpperInvariant()).Append('\n');

        foreach (var l in lines)
        {
            text.Append("L|").Append(l.SkuId.ToString("D")).Append('|').Append(l.CatId?.ToString("D")).Append('|').Append(l.Quantity.ToString(inv))
                .Append('|').Append(l.UnitPrice.Value.ToString(inv)).Append('|').Append(l.LineDiscount.Value.ToString(inv)).Append('|').Append(l.OrderDiscountShare.Value.ToString(inv))
                .Append('|').Append(l.TaxRatePercent.ToString("F2", inv)).Append('|').Append(l.TaxAmount.Value.ToString(inv)).Append('\n');
        }

        text.Append("T|").Append(totals.Subtotal.Value.ToString(inv)).Append('|').Append(totals.LineDiscountTotal.Value.ToString(inv)).Append('|').Append(totals.OrderDiscount.Value.ToString(inv))
            .Append('|').Append(totals.DeliveryCharge.Value.ToString(inv)).Append('|').Append(totals.TaxTotal.Value.ToString(inv)).Append('|').Append(totals.GrandTotal.Value.ToString(inv));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
