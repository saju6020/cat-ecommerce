using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Owned by HO-PHM-001 §8.10 (ADR-149). Read-only. Evaluated fresh on every call -- "today" is
/// computed each time so the gate locks again automatically at midnight (US-PHM-08 AC2), whether or
/// not any sweep has run.
///
/// HO-ORD-001 calls <see cref="CheckAsync"/> at add-to-cart and at checkout; HO-SHP-001 calls it for
/// purchasability. Until HO-PHM-001 lands, <c>Fakes/Pharmacy/DenyAllDietAuthorisationGate</c> denies
/// every check (<c>NoAuthorisation</c>) and returns no authorised cats -- the precedent is
/// HO-SHP-001 §8.9's own already-specified "fail-closed... the shipped stub denies everyone"
/// (ADR-084). Moved here, registered by <c>PharmacyModule</c>, per docs/handover/CONTRACTS.md's
/// "only the owning area registers its fakes" rule -- HO-SHP-001's own inline stub instructions
/// (§12.x, written before this contracts pass) are superseded by this single, centrally owned
/// registration.
/// </summary>
public interface IDietAuthorisationGate
{
    Task<DietGateResult> CheckAsync(Guid customerId, Guid catId, Guid skuId, CancellationToken ct);

    /// <summary>
    /// The gate's answer for ONE cat across MANY SKUs in a single call (HO-SHP-001 backlog row 20: the food
    /// recommendations used to make one <see cref="CheckAsync"/> per prescription SKU). The default
    /// implementation loops <see cref="CheckAsync"/> so existing implementations keep working unchanged;
    /// HO-PHM-001's real gate should override it with one set-based read. Every requested SKU is a key.
    /// </summary>
    async Task<IReadOnlyDictionary<Guid, DietGateResult>> CheckManyAsync(
        Guid customerId, Guid catId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
    {
        var results = new Dictionary<Guid, DietGateResult>();
        foreach (var skuId in skuIds.Distinct())
            results[skuId] = await CheckAsync(customerId, catId, skuId, ct);
        return results;
    }

    Task<IReadOnlyList<AuthorisedCat>> GetAuthorisedCatsAsync(Guid customerId, Guid skuId, CancellationToken ct);
}
