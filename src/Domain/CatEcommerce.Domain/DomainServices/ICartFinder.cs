namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §11.4 -- the cross-cart lookups a cart handler needs and the single-aggregate repository contract cannot express (several rows at once).
/// Ids only, <c>AsNoTracking</c>; the handler then loads each cart it mutates through its own repository (the precedent is
/// <see cref="IWaitlistEntryFinder"/>). Implemented in Infrastructure.
/// </summary>
public interface ICartFinder
{
    /// <summary>Active guest carts idle since before <paramref name="cutoff"/>, oldest first, at most <paramref name="take"/> (C8).</summary>
    Task<IReadOnlyList<Guid>> FindStaleGuestCartIdsAsync(DateTimeOffset cutoff, int take, CancellationToken ct);

    /// <summary>Every cart a customer holds, any status (a merge of customers moves them -- ADR-039).</summary>
    Task<IReadOnlyList<Guid>> FindCartIdsByCustomerAsync(Guid customerId, CancellationToken ct);
}
