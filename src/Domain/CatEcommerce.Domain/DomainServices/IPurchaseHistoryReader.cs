namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SHP-001 section 8.11 -- owned by HO-ORD-001. "Purchased" (OQ-13): a delivered online order
/// line or a completed POS sale. Shipped <c>NoPurchaseHistoryReader</c> (Infrastructure) returns an
/// empty set, so no review can be posted against the real purchase-history contract until
/// HO-ORD-001 replaces the registration (one line, ShopModule) -- I-D6 pins this fail-closed
/// default (ADR-086).
/// </summary>
public interface IPurchaseHistoryReader
{
    Task<IReadOnlySet<Guid>> GetPurchasedSkuIdsAsync(Guid customerId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct);
}
