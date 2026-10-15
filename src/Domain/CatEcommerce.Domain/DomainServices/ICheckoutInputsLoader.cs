using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.14 -- loads everything the pure <c>CheckoutPricing</c> needs for one customer and one set of checkout choices, in as few round trips as
/// possible: the cart, the SKU snapshots, the quantity that can be sold at the fulfilling branch, the prescription status of each prescription line,
/// the tax rates in force, the zone the address falls in with its service levels, the branch calendar, the discount code and the customer's use of it,
/// the payment switches, and the policies still to be accepted. Read-only (<c>AsNoTracking</c>, projections): the placement handler loads the discount
/// code as a <b>tracked</b> aggregate separately, because it mutates it. Implemented in Infrastructure.
/// </summary>
public interface ICheckoutInputsLoader
{
    Task<CheckoutInputs> LoadAsync(Guid customerId, FulfilmentChoice choice, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// HO-ORD-001 §8.11 -- the inputs of a <b>counter</b> sale: the lines the cashier scanned (not a cart), priced and capped at what the branch can sell, no delivery, no
    /// pending policies. <paramref name="customerId"/> is optional (an anonymous walk-in); a prescription line then has no authorisation and needs a customer and a cat.
    /// </summary>
    Task<CheckoutInputs> LoadCounterAsync(Guid branchId, Guid? customerId, IReadOnlyList<CounterLineRequest> lines, string? discountCode, DateTimeOffset now, CancellationToken ct);
}

/// <summary>A line the cashier scanned: the SKU, how many, and (for a prescription food) the cat it is for.</summary>
public sealed record CounterLineRequest(Guid SkuId, int Quantity, Guid? CatId);
