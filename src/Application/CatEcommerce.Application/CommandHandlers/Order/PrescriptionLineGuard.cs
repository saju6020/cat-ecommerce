using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Order.OrderHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>
/// HO-ORD-001 §11.4 C5 / §8.5 (US-SHP-10 AC2) -- the prescription rule for ONE line, shared by every call site (add to cart, change a line's cat,
/// re-order, and later the checkout quote and the point of sale) so they cannot drift: a prescription SKU needs a signed-in customer and a cat; the
/// cat must be <b>the caller's</b> (otherwise <c>CatNotOwnedByCaller</c>, answered <c>404</c> -- a customer must not learn that another customer's
/// cat exists); and <see cref="IDietAuthorisationGate"/> (HO-PHM-001) must say the authorisation is live today. Ordering owns no authorisation
/// store and never decides who may buy: it asks the gate, and a gate that cannot say "authorised" means "not authorised" (fail closed).
/// </summary>
public static class PrescriptionLineGuard
{
    /// <summary>Returns <c>null</c> when the line may proceed, else the violation to answer with.</summary>
    public static async Task<CommandResponse?> CheckAsync(
        Guid? customerId, Guid? catId, Guid skuId, IAggregateRootRepository<CatAggregate> cats, IDietAuthorisationGate gate, CancellationToken ct = default)
    {
        if (customerId is not { } customer || catId is not { } cat)
            return Violation(BusinessViolationCodes.PrescriptionCatRequired, "CatId", OrderMessages.PrescriptionCatRequired);

        var owned = await cats.GetByIdAsync(cat);
        if (owned is null || owned.OwnerId != customer)
            return Violation(BusinessViolationCodes.CatNotOwnedByCaller, "CatId", "Cat not found.");

        var result = await gate.CheckAsync(customer, cat, skuId, ct);
        return PrescriptionPurchaseGate.FromOutcome(result.Outcome).IsPermitted
            ? null
            : Violation(BusinessViolationCodes.PrescriptionNotAuthorised, "CatId", OrderMessages.PrescriptionNotAuthorised);
    }
}
