namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-SHP-001 section 8.4 -- the cross-aggregate facts the handler gathers before calling
/// <c>ProductAggregate.Publish</c>. <c>ProductPublicationRules.Evaluate</c> is pure and takes
/// exactly this plus the product itself.
/// </summary>
public sealed record PublicationFacts(
    IReadOnlyCollection<Guid> PricedSkuIds,
    bool TaxClassActive,
    IReadOnlyList<BundleComponentFact> BundleComponentFacts);
