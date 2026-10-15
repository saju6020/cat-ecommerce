using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Order.OrderHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>Who is acting on a cart: the signed-in customer (their one active cart, found across devices) or a guest (their cart, found by the hash
/// of the cookie). A staff session on a storefront cart route is treated as a guest -- it has no customer cart.</summary>
internal static class CartCaller
{
    public static Guid? CustomerId(Command command) =>
        command.UserContext is { UserId: var id } user && id != Guid.Empty && user.Roles.Contains("Customer", StringComparer.Ordinal) ? id : null;

    public static async Task<CartAggregate?> FindAsync(IAggregateRootRepository<CartAggregate> carts, Command command, string? guestTokenHash)
    {
        if (CustomerId(command) is { } customerId)
            return await carts.GetByFilterAsync(c => c.CustomerId == customerId && c.Status == CartStatus.Active);

        if (string.IsNullOrEmpty(guestTokenHash)) return null;

        return await carts.GetByFilterAsync(c => c.GuestTokenHash == guestTokenHash && c.Status == CartStatus.Active);
    }
}

/// <summary>
/// HO-ORD-001 §11.4 C5 (US-ORD-01, US-SHP-10 AC2). Resolve the caller's cart (create it on the first line); the SKU must be purchasable
/// (<c>SkuNotPurchasable</c>) and have stock to sell at the storefront branch (<c>SkuUnavailable</c> -- advisory: a cart reserves nothing); a
/// prescription SKU needs the signed-in customer, a cat that is theirs and a live authorisation for that cat (<see cref="PrescriptionLineGuard"/>);
/// then one mutation on the aggregate. A prescription line is keyed by cat; an ordinary SKU never carries one, so the same ordinary SKU is one line.
/// </summary>
public sealed class AddCartLineCommandHandler(
    IAggregateRootRepository<CartAggregate> carts,
    IAggregateRootRepository<CatAggregate> cats,
    ICatalogueLookup catalogue,
    ISkuAvailabilityReader availability,
    IDietAuthorisationGate dietGate,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AddCartLineCommand>
{
    public async Task<CommandResponse> HandleAsync(AddCartLineCommand command)
    {
        var now = timeProvider.GetUtcNow();
        var customerId = CartCaller.CustomerId(command);

        if (customerId is null && string.IsNullOrEmpty(command.GuestTokenHash) && string.IsNullOrEmpty(command.NewGuestTokenHash))
            return Violation(BusinessViolationCodes.CartNotFound, nameof(command.GuestTokenHash), "No cart was identified.");

        var skus = await catalogue.GetSkusAsync([command.SkuId], now, default);
        if (!skus.TryGetValue(command.SkuId, out var sku) || !sku.IsPurchasable || sku.CurrentPrice is null || sku.CurrencyCode is null)
            return Violation(BusinessViolationCodes.SkuNotPurchasable, nameof(command.SkuId), OrderMessages.SkuNotPurchasable);

        var status = await availability.GetAsync([command.SkuId], default);
        if (status.GetValueOrDefault(command.SkuId) != AvailabilityStatus.InStock)
            return Violation(BusinessViolationCodes.SkuUnavailable, nameof(command.SkuId), OrderMessages.SkuUnavailable);

        Guid? catId = null;
        if (sku.RequiresPrescription)
        {
            var refusal = await PrescriptionLineGuard.CheckAsync(customerId, command.CatId, command.SkuId, cats, dietGate);
            if (refusal is not null) return refusal;
            catId = command.CatId;
        }

        var cart = await CartCaller.FindAsync(carts, command, command.GuestTokenHash);
        var isNew = cart is null;
        if (cart is null)
        {
            cart = new CartAggregate(command.NewCartId);
            if (customerId is { } customer) cart.OpenForCustomer(customer, now, command.UserContext!);
            else if (!string.IsNullOrEmpty(command.NewGuestTokenHash)) cart.OpenForGuest(command.NewGuestTokenHash, now, command.UserContext!);
            else return Violation(BusinessViolationCodes.CartNotFound, nameof(command.GuestTokenHash), "No cart was identified.");
        }

        Guid lineId;
        try
        {
            lineId = cart.AddLine(
                command.SkuId, command.Quantity, catId, sku.CurrentPrice.Value, sku.CurrencyCode, now,
                settings.GetInt(SettingKeys.OrdersMaxQuantityPerLine), settings.GetInt(SettingKeys.OrdersMaxCartLines));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (isNew) await carts.SaveAsync(cart, EventPublishOption.InMemoryPublishOnly);
        else await carts.UpdateAsync(cart, EventPublishOption.InMemoryPublishOnly);

        return Success(new AddCartLineResult(lineId, cart.Id, GuestCartCreated: isNew && customerId is null));
    }
}

/// <summary>HO-ORD-001 §11.4 C6 -- change a line's quantity and/or cat on the caller's own cart. A cat change re-checks ownership and authorisation.</summary>
public sealed class UpdateCartLineCommandHandler(
    IAggregateRootRepository<CartAggregate> carts,
    IAggregateRootRepository<CatAggregate> cats,
    ICatalogueLookup catalogue,
    IDietAuthorisationGate dietGate,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<UpdateCartLineCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateCartLineCommand command)
    {
        var now = timeProvider.GetUtcNow();
        var cart = await CartCaller.FindAsync(carts, command, command.GuestTokenHash);
        var line = cart?.Lines.FirstOrDefault(l => l.Id == command.LineId);
        if (cart is null || line is null)
            return Violation(BusinessViolationCodes.CartLineNotFound, nameof(command.LineId), "That cart line was not found.");

        try
        {
            if (command.CatId is { } newCat && newCat != line.CatId)
            {
                var skus = await catalogue.GetSkusAsync([line.SkuId], now, default);
                if (skus.TryGetValue(line.SkuId, out var sku) && sku.RequiresPrescription)
                {
                    var refusal = await PrescriptionLineGuard.CheckAsync(CartCaller.CustomerId(command), newCat, line.SkuId, cats, dietGate);
                    if (refusal is not null) return refusal;
                    cart.SetLineCat(line.Id, newCat, now);
                }
            }

            if (command.Quantity is { } quantity)
                cart.SetQuantity(line.Id, quantity, now, settings.GetInt(SettingKeys.OrdersMaxQuantityPerLine));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await carts.UpdateAsync(cart, EventPublishOption.InMemoryPublishOnly);
        return Success(cart.Id);
    }
}

/// <summary>C6 -- remove a line. Idempotent: a line that is already gone (or never was) succeeds.</summary>
public sealed class RemoveCartLineCommandHandler(IAggregateRootRepository<CartAggregate> carts, TimeProvider timeProvider)
    : ICommandHandlerAsync<RemoveCartLineCommand>
{
    public async Task<CommandResponse> HandleAsync(RemoveCartLineCommand command)
    {
        var cart = await CartCaller.FindAsync(carts, command, command.GuestTokenHash);
        if (cart is null || !cart.RemoveLine(command.LineId, timeProvider.GetUtcNow())) return Success();

        await carts.UpdateAsync(cart, EventPublishOption.InMemoryPublishOnly);
        return Success(cart.Id);
    }
}

/// <summary>
/// HO-ORD-001 §11.4 C7 (ADR-199, US-ORD-01 AC1) -- merge the guest cart into the signed-in customer's cart. The guest cart (found by the cookie
/// hash, <c>Active</c>) is the <b>tracked</b> second root of this unit of work: the customer's cart is the saved one, and its save commits the guest
/// cart's <c>Merged</c> status too (V2). A missing or already merged guest cart succeeds with nothing written; the customer cart is created if none.
/// </summary>
public sealed class MergeGuestCartCommandHandler(
    IAggregateRootRepository<CartAggregate> carts,
    ICatalogueLookup catalogue,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<MergeGuestCartCommand>
{
    public async Task<CommandResponse> HandleAsync(MergeGuestCartCommand command)
    {
        var now = timeProvider.GetUtcNow();
        if (CartCaller.CustomerId(command) is not { } customerId)
            return Violation(BusinessViolationCodes.CartNotFound, nameof(command.GuestTokenHash), "Sign in to merge a cart.");

        var guest = await carts.GetByFilterAsync(c => c.GuestTokenHash == command.GuestTokenHash && c.Status == CartStatus.Active);
        if (guest is null) return Success(new MergeGuestCartResult(0, []));

        var customerCart = await carts.GetByFilterAsync(c => c.CustomerId == customerId && c.Status == CartStatus.Active);
        var isNew = customerCart is null;
        if (customerCart is null)
        {
            customerCart = new CartAggregate(command.NewCartId);
            customerCart.OpenForCustomer(customerId, now, command.UserContext!);
        }

        var result = customerCart.MergeFrom(guest, now, settings.GetInt(SettingKeys.OrdersMaxQuantityPerLine), settings.GetInt(SettingKeys.OrdersMaxCartLines));

        if (isNew) await carts.SaveAsync(customerCart, EventPublishOption.InMemoryPublishOnly);
        else await carts.UpdateAsync(customerCart, EventPublishOption.InMemoryPublishOnly);

        var names = result.DroppedSkuIds.Count == 0
            ? new Dictionary<Guid, Domain.Models.OrderCatalogueSku>()
            : (await catalogue.GetSkusAsync(result.DroppedSkuIds, now, default)).ToDictionary(kv => kv.Key, kv => kv.Value);

        var dropped = result.DroppedSkuIds
            .Select(id => new DroppedCartLine(names.TryGetValue(id, out var sku) ? sku.ProductName : "An item", "CartFull"))
            .ToList();

        return Success(new MergeGuestCartResult(result.MergedLineCount, dropped));
    }
}

/// <summary>HO-ORD-001 §11.4 C8 -- abandon one batch of stale guest carts. All the batch's changes are tracked on the shared context and committed by
/// one save; the worker calls this until a pass abandons fewer than the batch size.</summary>
public sealed class PurgeStaleGuestCartsCommandHandler(
    IAggregateRootRepository<CartAggregate> carts, ICartFinder finder, IBusinessSettings settings, TimeProvider timeProvider)
    : ICommandHandlerAsync<PurgeStaleGuestCartsCommand>
{
    public async Task<CommandResponse> HandleAsync(PurgeStaleGuestCartsCommand command)
    {
        var now = timeProvider.GetUtcNow();
        var cutoff = now.AddDays(-settings.GetInt(SettingKeys.OrdersGuestCartRetentionDays));
        var ids = await finder.FindStaleGuestCartIdsAsync(cutoff, CartRules.PurgeBatchSize, default);

        CartAggregate? last = null;
        var abandoned = 0;
        foreach (var id in ids)
        {
            var cart = await carts.GetByIdAsync(id);
            if (cart is null || cart.Status != CartStatus.Active) continue;

            cart.Abandon(now);
            last = cart;
            abandoned++;
        }

        if (last is not null) await carts.UpdateAsync(last, EventPublishOption.InMemoryPublishOnly); // commits every tracked cart of the batch
        return Success(new PurgeStaleGuestCartsResult(abandoned));
    }
}
