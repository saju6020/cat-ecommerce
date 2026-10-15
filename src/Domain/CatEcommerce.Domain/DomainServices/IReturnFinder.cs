namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.10, §11.6 -- the cross-return lookups a handler needs and the single-aggregate repository contract cannot express: ids and quantities only,
/// <c>AsNoTracking</c>. Implemented in Infrastructure.
/// </summary>
public interface IReturnFinder
{
    /// <summary>Per order line, the quantity asked for in returns that are still <c>Requested</c> or <c>Approved</c> (not yet received, rejected or closed) -- what a new
    /// request must leave room for (<c>ReturnQuantityExceeded</c>).</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetOpenQuantitiesAsync(Guid orderId, CancellationToken ct);

    /// <summary>Every return a customer has (a customer merge moves them, HO-ACC-001 ADR-039).</summary>
    Task<IReadOnlyList<Guid>> FindReturnIdsByCustomerAsync(Guid customerId, CancellationToken ct);
}
