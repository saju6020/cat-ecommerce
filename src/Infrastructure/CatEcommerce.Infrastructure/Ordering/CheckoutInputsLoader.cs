using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>
/// HO-ORD-001 §12.2, §8.14 -- <see cref="ICheckoutInputsLoader"/>. Everything is a projection (<c>AsNoTracking</c>, ADR-012): the cart and its lines, the SKU
/// snapshots (<see cref="ICatalogueLookup"/>), the quantity that can be sold at the <b>fulfilling</b> branch (the zone's branch for a delivery, the chosen
/// branch for a pickup, else the storefront branch), the tax rate in force at <c>now</c> per tax class (HO-ADM-001 ADR-049: read at transaction time and
/// snapshotted on the line), the zone an address resolves to with its service levels, the branch calendar, the discount code with the customer's use of it,
/// the payment switches and the bank account, and the policies still to accept. The pricing is then a pure function of the result.
/// </summary>
public sealed class CheckoutInputsLoader(
    CatEcommerceDbContext db,
    ICatalogueLookup catalogue,
    IDietAuthorisationGate dietGate,
    ICustomerDirectory customers,
    ICustomerPolicyStatus policies,
    IBusinessSettings settings,
    IBranchCalendar calendar,
    IOptions<ShopOptions> shopOptions,
    IOrderBillingReader billingReader,
    ILogger<CheckoutInputsLoader> logger)
    : ICheckoutInputsLoader
{
    private const int CalendarHorizonDays = 35;

    public async Task<CheckoutInputs> LoadAsync(Guid customerId, FulfilmentChoice choice, DateTimeOffset now, CancellationToken ct)
    {
        var currency = settings.GetString(SettingKeys.CommerceCurrencyCode);

        var cartLines = await db.Carts.AsNoTracking()
            .Where(c => c.CustomerId == customerId && c.Status == CartStatus.Active)
            .SelectMany(c => c.Lines.OrderBy(l => l.AddedOn).ThenBy(l => l.Id))
            .Select(l => new { l.Id, l.SkuId, l.CatId, l.Quantity, l.PriceWhenAdded })
            .ToListAsync(ct);

        var address = choice.AddressId is { } addressId ? await customers.GetAddressAsync(customerId, addressId, ct) : null;
        var zone = address is null ? null : await ResolveZoneAsync(address, ct);

        var pickupBranches = await db.Set<BranchAggregate>().AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new PickupBranchFact(b.Id, b.Name, b.AddressLine, b.PhoneNumber))
            .ToListAsync(ct);

        var fulfilmentBranchId =
            choice.Method == FulfilmentMethod.Delivery && zone is not null ? zone.BranchId
            : choice.Method == FulfilmentMethod.Pickup && choice.PickupBranchId is { } picked && pickupBranches.Any(b => b.BranchId == picked) ? picked
            : shopOptions.Value.EffectiveStorefrontBranchId;

        var (todayLocal, openDates) = await CalendarAsync(zone?.BranchId ?? fulfilmentBranchId, now, ct);

        var skus = await catalogue.GetSkusAsync(cartLines.Select(l => l.SkuId).Distinct().ToList(), now, ct);
        WarnOnceWhenWeightIsUnknown(skus.Values);
        var sellable = await SellableQuantities.ForAsync(db, skus.Values.ToList(), fulfilmentBranchId, ct);
        var rates = await TaxRatesAsync(skus.Values.Select(s => s.TaxClassId).Distinct().ToList(), now, ct);

        var lines = new List<CheckoutLineInput>();
        foreach (var line in cartLines)
        {
            if (!skus.TryGetValue(line.SkuId, out var sku)) continue; // a SKU that no longer exists at all drops out of the quote; the cart keeps the line

            lines.Add(new CheckoutLineInput(
                line.Id, sku, line.CatId, line.Quantity, line.PriceWhenAdded, sellable.GetValueOrDefault(sku.SkuId), rates.TryGetValue(sku.TaxClassId, out var rate) ? rate : null,
                await PrescriptionStatusAsync(customerId, sku, line.CatId, ct)));
        }

        var (discount, facts) = await DiscountAsync(customerId, choice.DiscountCode, ct);

        return new CheckoutInputs(
            now, todayLocal, currency, lines, choice, address, zone, openDates, pickupBranches, choice.DiscountCode, discount, facts,
            await PaymentSettingsAsync(fulfilmentBranchId, ct), await policies.GetPendingAsync(customerId, ct), fulfilmentBranchId,
            choice.Method == FulfilmentMethod.Delivery ? await billingReader.GetDeliveryTaxAsync(now, ct) : null);
    }

    public async Task<CheckoutInputs> LoadCounterAsync(
        Guid branchId, Guid? customerId, IReadOnlyList<CounterLineRequest> requested, string? discountCode, DateTimeOffset now, CancellationToken ct)
    {
        var currency = settings.GetString(SettingKeys.CommerceCurrencyCode);
        var choice = new FulfilmentChoice(FulfilmentMethod.Counter, null, null, null, null, null, OrderPaymentMethod.InStore, string.IsNullOrWhiteSpace(discountCode) ? null : discountCode.Trim());

        var (todayLocal, _) = await CalendarAsync(branchId, now, ct);
        var skus = await catalogue.GetSkusAsync(requested.Select(l => l.SkuId).Distinct().ToList(), now, ct);
        var sellable = await SellableQuantities.ForAsync(db, skus.Values.ToList(), branchId, ct);
        var rates = await TaxRatesAsync(skus.Values.Select(s => s.TaxClassId).Distinct().ToList(), now, ct);

        var lines = new List<CheckoutLineInput>();
        foreach (var line in requested)
        {
            if (!skus.TryGetValue(line.SkuId, out var sku)) continue; // an unknown SKU drops out of the quote and is reported as excluded by the caller

            var prescription = !sku.RequiresPrescription ? PrescriptionLineStatus.NotRequired
                : customerId is null || line.CatId is null ? PrescriptionLineStatus.CatRequired
                : await PrescriptionStatusAsync(customerId.Value, sku, line.CatId, ct);

            lines.Add(new CheckoutLineInput(
                Guid.NewGuid(), sku, line.CatId, line.Quantity, sku.CurrentPrice ?? 0m, sellable.GetValueOrDefault(sku.SkuId), rates.TryGetValue(sku.TaxClassId, out var rate) ? rate : null, prescription));
        }

        var (discount, facts) = await DiscountAsync(customerId, choice.DiscountCode, ct);

        return new CheckoutInputs(
            now, todayLocal, currency, lines, choice, null, null, new HashSet<DateOnly>(), [], choice.DiscountCode, discount, facts,
            new PaymentSettingsFacts(false, 0m, false, false, false), [], branchId);
    }

    private static int _weightWarningLogged;

    /// <summary>
    /// Owner decision 7 (2026-10-09): the zone's weight bands are applied to the cart's total shipping weight. Shop's SKU shipping weight (<c>ShippingWeightGrams</c>,
    /// HO-SHP-001-FU3) is not populated yet, so a SKU without one counts as 0 grams and the bands cannot add a surcharge -- logged <b>once per process</b>, with no
    /// SKU or customer data, so the gap is visible without flooding the log.
    /// </summary>
    private void WarnOnceWhenWeightIsUnknown(IEnumerable<OrderCatalogueSku> skus)
    {
        if (skus.All(s => s.ShippingWeightGrams is not null)) return;
        if (Interlocked.Exchange(ref _weightWarningLogged, 1) == 1) return;

        logger.LogWarning("Some SKUs in a checkout have no shipping weight (Shop's ShippingWeightGrams is not populated); they count as 0 g, so delivery weight bands add no surcharge for them.");
    }

    private async Task<PrescriptionLineStatus> PrescriptionStatusAsync(Guid customerId, OrderCatalogueSku sku, Guid? catId, CancellationToken ct)
    {
        if (!sku.RequiresPrescription) return PrescriptionLineStatus.NotRequired;
        if (catId is null) return PrescriptionLineStatus.CatRequired;

        var result = await dietGate.CheckAsync(customerId, catId.Value, sku.SkuId, ct);
        return PrescriptionPurchaseGate.FromOutcome(result.Outcome).IsPermitted ? PrescriptionLineStatus.Authorised : PrescriptionLineStatus.NotAuthorised;
    }

    private async Task<DeliveryZoneFacts?> ResolveZoneAsync(AddressFacts address, CancellationToken ct)
    {
        var zones = await db.DeliveryZones.AsNoTracking()
            .Where(z => z.IsActive)
            .Select(z => new
            {
                z.Id, z.Name, z.Priority, z.BranchId, z.CodEnabled, z.CodMaxOrderValue,
                Coverage = z.Coverage.Select(c => new { c.District, c.PostalCodes }).ToList(),
                Services = z.Services.Select(s => new { s.ServiceLevel, s.IsActive, s.BaseCharge, s.FreeOverOrderValue, s.LeadTimeDays, s.WeightBands, s.SlotWindows }).ToList(),
            })
            .ToListAsync(ct);

        var resolvable = zones.Select(z => new ResolvableZone(z.Id, z.Name, z.Priority, true, z.Coverage.Select(c => new CoverageRule(c.District, c.PostalCodes)).ToList())).ToList();
        var resolved = DeliveryZoneResolver.Resolve(resolvable, address.District, address.PostalCode);
        if (resolved is null) return null;

        var chosen = zones.Single(z => z.Id == resolved.ZoneId);
        return new DeliveryZoneFacts(
            chosen.Id, chosen.Name, chosen.BranchId, chosen.CodEnabled, chosen.CodMaxOrderValue,
            chosen.Services.Select(s => new ZoneServiceFacts(s.ServiceLevel, s.IsActive, new ZoneChargeRules(s.BaseCharge, s.FreeOverOrderValue, s.WeightBands), s.LeadTimeDays, s.SlotWindows)).ToList());
    }

    /// <summary>"Today" in the branch's own zone, and the local dates the branch is open on over the next five weeks (HO-ADM-001 <c>IBranchCalendar</c>: opening
    /// hours minus closures; a branch with no hours set is closed and offers no slot).</summary>
    private async Task<(DateOnly Today, IReadOnlySet<DateOnly> Open)> CalendarAsync(Guid branchId, DateTimeOffset now, CancellationToken ct)
    {
        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == branchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync(ct) ?? "UTC";
        var today = BranchCalendar.LocalDateOf(now, timeZoneId);

        var intervals = await calendar.OpenIntervalsAsync(branchId, today, today.AddDays(CalendarHorizonDays), ct);
        return (today, intervals.Select(i => BranchCalendar.LocalDateOf(i.Start, timeZoneId)).ToHashSet());
    }

    private async Task<Dictionary<Guid, decimal>> TaxRatesAsync(IReadOnlyCollection<Guid> taxClassIds, DateTimeOffset now, CancellationToken ct)
    {
        if (taxClassIds.Count == 0) return [];

        var rows = await db.Set<TaxRateAggregate>().AsNoTracking()
            .Where(r => taxClassIds.Contains(r.TaxClassId) && r.EffectiveFrom <= now)
            .Select(r => new { r.TaxClassId, r.RatePercent, r.EffectiveFrom })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.TaxClassId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.EffectiveFrom).First().RatePercent);
    }

    private async Task<(DiscountRuleSet? Code, CustomerDiscountFacts Facts)> DiscountAsync(Guid? customerId, string? codeText, CancellationToken ct)
    {
        var hasPrior = customerId is not null && await db.Orders.AsNoTracking().AnyAsync(o => o.CustomerId == customerId && o.Status != OrderStatus.Cancelled, ct);
        if (string.IsNullOrWhiteSpace(codeText)) return (null, new CustomerDiscountFacts(0, hasPrior));

        var normalised = Domain.Aggregates.Order.DiscountCodeAggregate.Normalise(codeText);
        var row = await db.DiscountCodes.AsNoTracking()
            .Where(c => c.Code == normalised)
            .Select(c => new
            {
                c.Id, c.Code, c.Kind, c.Percent, c.Amount, c.MinimumSpend, c.Scope, c.ProductIds, c.CategoryIds, c.FirstOrderOnly, c.UsageLimit, c.TimesRedeemed,
                c.PerCustomerLimit, c.ValidFrom, c.ValidTo, c.IsActive, c.CurrencyCode,
            })
            .FirstOrDefaultAsync(ct);

        if (row is null) return (null, new CustomerDiscountFacts(0, hasPrior));

        var used = customerId is null ? 0 : await db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == customerId && o.DiscountCodeId == row.Id && !o.DiscountReleased, ct);
        var rules = new DiscountRuleSet(
            row.Id, row.Code, row.Kind, row.Percent is { } p ? MinorUnits.PercentToHundredths(p) : null, row.Amount is { } a ? MinorUnits.FromAmount(a) : null,
            row.MinimumSpend is { } m ? MinorUnits.FromAmount(m) : null, row.Scope, row.ProductIds.ToHashSet(), row.CategoryIds.ToHashSet(), row.FirstOrderOnly,
            row.UsageLimit, row.TimesRedeemed, row.PerCustomerLimit, row.ValidFrom, row.ValidTo, row.IsActive, row.CurrencyCode);

        return (rules, new CustomerDiscountFacts(used, hasPrior));
    }

    private async Task<PaymentSettingsFacts> PaymentSettingsAsync(Guid branchId, CancellationToken ct) =>
        new(
            settings.GetBool(SettingKeys.OrdersCodEnabled, branchId), settings.GetDecimal(SettingKeys.OrdersCodMaxOrderValue, branchId),
            settings.GetBool(SettingKeys.OrdersBankTransferEnabled, branchId),
            await db.BankTransferAccounts.AsNoTracking().AnyAsync(a => a.Id == Domain.Aggregates.Order.BankTransferAccountAggregate.SingletonId && a.IsActive, ct),
            settings.GetBool(SettingKeys.OrdersPayOnCollectionEnabled, branchId));
}
