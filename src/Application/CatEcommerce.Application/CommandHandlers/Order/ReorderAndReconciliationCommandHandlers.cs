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

/// <summary>
/// HO-ORD-001 §11.4 C15 (US-ORD-19 AC3, US-POR-02) -- "order again". Own order only. Each line goes back into the customer's one active cart (created if
/// they have none) at <b>today's price</b>: it must still be purchasable, in stock and -- for a prescription line -- still authorised for that line's cat
/// (the same <see cref="PrescriptionLineGuard"/> as add-to-cart); any other line is listed with its reason (an archived SKU is <c>NotPurchasable</c>).
/// <b>No order is created</b>; the customer reviews the cart and checks out as usual.
/// </summary>
public sealed class ReorderCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IAggregateRootRepository<CartAggregate> carts,
    IAggregateRootRepository<CatAggregate> cats,
    ICatalogueLookup catalogue,
    ISkuAvailabilityReader availability,
    IDietAuthorisationGate dietGate,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ReorderCommand>
{
    public async Task<CommandResponse> HandleAsync(ReorderCommand command)
    {
        if (CartCaller.CustomerId(command) is not { } customerId) return OrderAccess.NotFound();

        var order = await OrderAccess.ForCustomerAsync(orders, command.SourceOrderId, customerId);
        if (order is null) return OrderAccess.NotFound();

        var now = timeProvider.GetUtcNow();
        var skus = await catalogue.GetSkusAsync(order.Lines.Select(l => l.SkuId).Distinct().ToList(), now, default);
        var stock = await availability.GetAsync(order.Lines.Select(l => l.SkuId).Distinct().ToList(), default);

        var cart = await carts.GetByFilterAsync(c => c.CustomerId == customerId && c.Status == CartStatus.Active);
        var isNew = cart is null;
        if (cart is null)
        {
            cart = new CartAggregate(Guid.NewGuid());
            cart.OpenForCustomer(customerId, now, command.UserContext!);
        }

        var maxQuantity = settings.GetInt(SettingKeys.OrdersMaxQuantityPerLine);
        var maxLines = settings.GetInt(SettingKeys.OrdersMaxCartLines);
        var added = 0;
        var unavailable = new List<UnavailableReorderLine>();

        foreach (var line in order.Lines)
        {
            if (!skus.TryGetValue(line.SkuId, out var sku) || !sku.IsPurchasable || sku.CurrentPrice is null || sku.CurrencyCode is null)
            {
                unavailable.Add(new UnavailableReorderLine(line.ProductName, "NotPurchasable"));
                continue;
            }

            if (stock.GetValueOrDefault(line.SkuId) != AvailabilityStatus.InStock)
            {
                unavailable.Add(new UnavailableReorderLine(line.ProductName, "OutOfStock"));
                continue;
            }

            Guid? catId = null;
            if (sku.RequiresPrescription)
            {
                if (await PrescriptionLineGuard.CheckAsync(customerId, line.CatId, line.SkuId, cats, dietGate) is not null)
                {
                    unavailable.Add(new UnavailableReorderLine(line.ProductName, "PrescriptionNotAuthorised"));
                    continue;
                }

                catId = line.CatId;
            }

            try
            {
                cart.AddLine(line.SkuId, Math.Min(line.Quantity, maxQuantity), catId, sku.CurrentPrice.Value, sku.CurrencyCode, now, maxQuantity, maxLines);
                added++;
            }
            catch (DomainRuleViolationException ex)
            {
                unavailable.Add(new UnavailableReorderLine(line.ProductName, ex.ErrorCode));
            }
        }

        if (added > 0)
        {
            if (isNew) await carts.SaveAsync(cart, EventPublishOption.InMemoryPublishOnly);
            else await carts.UpdateAsync(cart, EventPublishOption.InMemoryPublishOnly);
        }

        return Success(new ReorderResult(added, unavailable));
    }
}

/// <summary>C27 (US-ORD-08 AC2) -- an accountant closes an <c>Open</c> reconciliation issue with a note. Resolving twice is refused.</summary>
public sealed class ResolveReconciliationIssueCommandHandler(IAggregateRootRepository<OrderReconciliationIssueAggregate> issues, TimeProvider timeProvider)
    : ICommandHandlerAsync<ResolveReconciliationIssueCommand>
{
    public async Task<CommandResponse> HandleAsync(ResolveReconciliationIssueCommand command)
    {
        var issue = await issues.GetByIdAsync(command.IssueId);
        if (issue is null || issue.BranchId != command.BranchId) return Violation(BusinessViolationCodes.ReconciliationIssueNotFound, nameof(command.IssueId), "That issue was not found.");

        try
        {
            issue.Resolve(command.UserContext!.UserId, command.Note, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await issues.UpdateAsync(issue, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
