namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SHP-001 section 8.10 / OQ-14 -- read-only: the names of the NON-archived bundles that contain
/// any of the given SKUs as a component. The handler passes them to the aggregate, which refuses to
/// flag such a product as prescription (a bundle may never contain a prescription diet).
/// </summary>
public interface IBundleMembershipReader
{
    Task<IReadOnlyList<string>> FindActiveBundleNamesContainingAsync(IReadOnlyCollection<Guid> componentSkuIds, CancellationToken ct = default);
}
