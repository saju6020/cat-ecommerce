using System.Globalization;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Order;

/// <summary>
/// HO-ORD-001 §12.6 (Phase D), US-ORD-02 to US-ORD-07 -- the checkout quote. The inputs come from <see cref="ICheckoutInputsLoader"/>, the price from the pure
/// <see cref="CheckoutPricing"/>; this handler only maps the result to the wire shape (money as <c>{ amount, currencyCode }</c>, amount a string) and attaches
/// the names of the caller's own cats. Nothing is priced in SQL, nothing is stored, and no aggregate is loaded.
/// </summary>
public sealed class GetCheckoutQuoteQueryHandler(CatEcommerceDbContext db, ICheckoutInputsLoader loader, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetCheckoutQuoteQuery, CheckoutQuoteViewModel>
{
    public async Task<QueryResponse<CheckoutQuoteViewModel>> HandleAsync(GetCheckoutQuoteQuery query)
    {
        var inputs = await loader.LoadAsync(query.CustomerId, query.Choice, timeProvider.GetUtcNow(), default);
        var quote = CheckoutPricing.Quote(inputs);
        var currency = quote.CurrencyCode;

        var catIds = quote.Lines.Where(l => l.CatId != null).Select(l => l.CatId!.Value)
            .Concat(quote.ExcludedLines.Where(l => l.CatId != null).Select(l => l.CatId!.Value)).Distinct().ToList();
        var cats = catIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<CatAggregate>().AsNoTracking().Where(c => catIds.Contains(c.Id) && c.OwnerId == query.CustomerId).Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name);

        CartCatViewModel? Cat(Guid? id) => id is { } catId && cats.TryGetValue(catId, out var name) ? new CartCatViewModel(catId, name) : null;
        MoneyViewModel Money(MinorUnits amount) => OrderMoney.Of(amount.ToAmount(), currency);

        var view = new CheckoutQuoteViewModel(
            quote.Lines.Select(l => new CheckoutQuoteLineViewModel(
                l.LineRef, l.SkuId, l.ProductName, l.VariantLabel, Cat(l.CatId), l.Quantity, Money(l.UnitPrice), Money(l.Subtotal), Money(l.LineDiscount),
                Money(l.OrderDiscountShare), l.TaxRatePercent, Money(l.TaxAmount), Money(l.LineTotal))).ToList(),
            quote.ExcludedLines.Select(l => new ExcludedLineViewModel(l.LineRef, l.SkuId, l.ProductName, Cat(l.CatId), l.CartQuantity, l.ReasonCode)).ToList(),
            quote.Changes.Select(c => new CheckoutChangeViewModel(c.Kind.DisplayName, c.LineRef, c.ProductName, c.Before, c.After, c.Message)).ToList(),
            new QuoteDeliveryViewModel(
                quote.Delivery.Available, quote.Delivery.UnavailableReason, quote.Delivery.ZoneId, quote.Delivery.ZoneName,
                quote.Delivery.ServiceLevels.Select(s => new QuoteServiceLevelViewModel(
                    s.Level.DisplayName, Money(s.Charge), s.LeadTimeDays,
                    s.Slots.Select(x => new DeliverySlotViewModel(
                        x.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), x.WindowStart.ToString("HH:mm", CultureInfo.InvariantCulture),
                        x.WindowEnd.ToString("HH:mm", CultureInfo.InvariantCulture), x.Label)).ToList())).ToList()),
            quote.SelectedServiceLevel?.DisplayName,
            quote.PickupBranches.Select(b => new PickupBranchViewModel(b.BranchId, b.Name, b.Address, b.Phone)).ToList(),
            quote.PaymentOptions.Select(o => new PaymentOptionViewModel(o.Method.DisplayName, o.Available, o.UnavailableReason, o.Message)).ToList(),
            quote.Discount is null ? null : new QuoteDiscountViewModel(quote.Discount.Code, quote.Discount.Applied, quote.Discount.ReasonCode, quote.Discount.Message),
            new QuoteTotalsViewModel(
                Money(quote.Totals.Subtotal), Money(quote.Totals.LineDiscountTotal), Money(quote.Totals.OrderDiscount), Money(quote.Totals.DeliveryCharge),
                Money(quote.Totals.TaxTotal), Money(quote.Totals.GrandTotal)),
            quote.PendingPolicies.Select(p => new PendingPolicyViewModel(p.Key, p.VersionNumber, p.Title)).ToList(),
            quote.CanPlace, quote.BlockingReasons, quote.Fingerprint, currency);

        return new QueryResponse<CheckoutQuoteViewModel> { Result = view };
    }
}
