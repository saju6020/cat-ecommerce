namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §11.6 -- the cross-order lookups an event handler needs and the single-aggregate repository contract cannot express (several rows at once):
/// ids only, <c>AsNoTracking</c>; the handler then loads each order it mutates through its own repository (the precedent is <see cref="ICartFinder"/>).
/// Implemented in Infrastructure.
/// </summary>
public interface IOrderFinder
{
    /// <summary>Every order a customer has, any status -- a merge of customers reassigns them (HO-ACC-001 ADR-039), an anonymisation flags them (ADR-214).</summary>
    Task<IReadOnlyList<Guid>> FindOrderIdsByCustomerAsync(Guid customerId, CancellationToken ct);

    /// <summary>The order dispatched with this tracking number (the webhook lookup, <c>ix_sales_order_tracking</c>), or <c>null</c>.</summary>
    Task<Guid?> FindOrderIdByTrackingNumberAsync(string trackingNumber, CancellationToken ct);
}
