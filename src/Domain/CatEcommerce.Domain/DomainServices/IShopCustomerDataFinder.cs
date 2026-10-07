namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SHP-001 section 11.6 -- HO-ACC-001 <c>CustomerMergedEvent</c> / <c>CustomerAccountAnonymisedEvent</c>
/// are binding on every context holding a customer id (ADR-039, ADR-041). The repository contract
/// loads one aggregate by id; moving every alert and review of a customer needs "find every
/// matching row" -- the same gap <see cref="IWaitlistEntryFinder"/> and
/// <see cref="IFlaggableAppointmentFinder"/> exist for. Ids only; read-only.
/// </summary>
public interface IShopCustomerDataFinder
{
    /// <summary>Every stock alert the customer holds, any status.</summary>
    Task<IReadOnlyList<Guid>> FindStockAlertIdsAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Every review of the customer that is not already withdrawn.</summary>
    Task<IReadOnlyList<Guid>> FindActiveReviewIdsAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Every review of the customer that is NOT published (pending, rejected, hidden, withdrawn):
    /// the ones whose text anonymisation must clear (row 21; ADR-096 keeps only published reviews).</summary>
    Task<IReadOnlyList<Guid>> FindUnpublishedReviewIdsAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Pending alerts for a SKU (discontinuation) or for every SKU of a product (archive).</summary>
    Task<IReadOnlyList<Guid>> FindPendingStockAlertIdsBySkuAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default);
}
