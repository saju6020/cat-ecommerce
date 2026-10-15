using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Order;

/// <summary>
/// HO-ORD-001 §12.6 (Phase C), US-ORD-01 -- the caller's cart as the storefront shows it. Reads are projections only (ADR-012): the cart and its lines
/// by column, the SKUs through <see cref="ICatalogueLookup"/>, stock from <c>inventory.v_sku_sellable</c> at the storefront branch (advisory --
/// a cart reserves nothing), cat names from a projection of the caller's own cats, and prescription status from <see cref="IDietAuthorisationGate"/>.
/// No cookie and no session is an empty cart.
/// </summary>
public sealed class GetCartQueryHandler(
    CatEcommerceDbContext db,
    ICatalogueLookup catalogue,
    IDietAuthorisationGate dietGate,
    IBusinessSettings settings,
    IOptions<ShopOptions> shopOptions,
    TimeProvider timeProvider)
    : IQueryHandlerAsync<GetCartQuery, CartViewModel>
{
    public async Task<QueryResponse<CartViewModel>> HandleAsync(GetCartQuery query)
    {
        var now = timeProvider.GetUtcNow();
        var currency = settings.GetString(SettingKeys.CommerceCurrencyCode);
        var maxQuantity = settings.GetInt(SettingKeys.OrdersMaxQuantityPerLine);

        var cartRows = query.CustomerId is { } customerId
            ? db.Carts.AsNoTracking().Where(c => c.CustomerId == customerId && c.Status == CartStatus.Active)
            : db.Carts.AsNoTracking().Where(c => c.GuestTokenHash == query.GuestTokenHash && c.Status == CartStatus.Active && query.GuestTokenHash != null);

        var cart = await cartRows
            .Select(c => new
            {
                c.Id,
                Lines = c.Lines.OrderBy(l => l.AddedOn).ThenBy(l => l.Id)
                    .Select(l => new { l.Id, l.SkuId, l.CatId, l.Quantity, l.PriceWhenAdded, l.CurrencyCode }).ToList(),
            })
            .FirstOrDefaultAsync();

        var guestPending = query.CustomerId is not null && !string.IsNullOrEmpty(query.GuestTokenHash)
            && await db.Carts.AsNoTracking().AnyAsync(c => c.GuestTokenHash == query.GuestTokenHash && c.Status == CartStatus.Active);

        if (cart is null)
            return Respond(new CartViewModel(null, [], 0, OrderMoney.Of(0m, currency), maxQuantity, guestPending));

        var skuIds = cart.Lines.Select(l => l.SkuId).Distinct().ToList();
        var skus = await catalogue.GetSkusAsync(skuIds, now, default);

        var branchId = shopOptions.Value.EffectiveStorefrontBranchId;
        var status = await SkuSellability.AvailabilityAsync(db, skuIds, now, branchId);
        var stock = await SkuSellability.StockAsync(db, skuIds, branchId);

        var catIds = cart.Lines.Where(l => l.CatId != null).Select(l => l.CatId!.Value).Distinct().ToList();
        var catNames = query.CustomerId is { } owner && catIds.Count > 0
            ? await db.Set<CatAggregate>().AsNoTracking().Where(c => catIds.Contains(c.Id) && c.OwnerId == owner).Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name)
            : [];

        var lines = new List<CartLineViewModel>();
        var subtotal = MinorUnits.Zero;
        var itemCount = 0;

        foreach (var line in cart.Lines)
        {
            skus.TryGetValue(line.SkuId, out var sku);
            var purchasable = sku is { IsPurchasable: true, CurrentPrice: not null };
            var unit = purchasable ? sku!.CurrentPrice : null;

            var inStock = purchasable && status.GetValueOrDefault(line.SkuId) == AvailabilityStatus.InStock;
            var available = sku is { IsBundle: false } && stock.TryGetValue(line.SkuId, out var figure) ? (int?)Math.Max(0, (int)Math.Floor(figure.SellableQuantity)) : null;
            var availability = !inStock ? "Unavailable" : available is { } left && left < line.Quantity ? "Limited" : "InStock";

            string? prescription = null;
            if (sku is { RequiresPrescription: true })
            {
                prescription = line.CatId is null || query.CustomerId is null
                    ? "CatRequired"
                    : PrescriptionPurchaseGate.FromOutcome((await dietGate.CheckAsync(query.CustomerId.Value, line.CatId.Value, line.SkuId, default)).Outcome).IsPermitted
                        ? "Authorised"
                        : "NotAuthorised";
            }

            MoneyViewModel? lineSubtotal = null;
            if (unit is { } price)
            {
                var amount = MinorUnits.FromAmount(price).Times(line.Quantity);
                lineSubtotal = OrderMoney.Of(amount.ToAmount(), sku!.CurrencyCode!);
                subtotal += amount;
            }

            itemCount += line.Quantity;
            lines.Add(new CartLineViewModel(
                line.Id, line.SkuId, sku?.ProductId ?? Guid.Empty, sku?.ProductName ?? "Unavailable item", sku?.VariantLabel ?? string.Empty, null, line.Quantity,
                line.CatId is { } catId && catNames.TryGetValue(catId, out var name) ? new CartCatViewModel(catId, name) : null,
                unit is { } p ? OrderMoney.Of(p, sku!.CurrencyCode!) : null, OrderMoney.Of(line.PriceWhenAdded, line.CurrencyCode),
                unit is { } current && current != line.PriceWhenAdded, availability, available, sku?.RequiresPrescription ?? false, prescription, lineSubtotal));
        }

        var lineCurrency = cart.Lines.Count > 0 ? cart.Lines[0].CurrencyCode : currency;
        return Respond(new CartViewModel(cart.Id, lines, itemCount, OrderMoney.Of(subtotal.ToAmount(), lineCurrency), maxQuantity, guestPending));
    }

    private static QueryResponse<CartViewModel> Respond(CartViewModel view) => new() { Result = view };
}
