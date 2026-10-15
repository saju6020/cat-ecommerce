namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-PHM-001 §11.6 (ADR-152) -- finds the ids of the pharmacy records held under a customer id, for the customer-merge handler. Read-only. <b>Register entries are deliberately not
/// listed here: they and their seals are never rewritten</b> -- reads resolve a merged customer id to the survivor instead.
/// </summary>
public interface IPharmacyRecordFinder
{
    Task<IReadOnlyList<Guid>> FindPrescriptionIdsAsync(Guid customerId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> FindDispensingIdsAsync(Guid customerId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> FindDietAuthorisationIdsAsync(Guid customerId, CancellationToken ct);
}
