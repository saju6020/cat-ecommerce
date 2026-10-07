using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Owned by HO-BRD-001 §8.14/§12.8 E2, consumed by HO-EMR-001 and HO-SRG-001: "Before recording a
/// charge on a visit with a <c>BoardingStayId</c>, calls
/// <c>IBoardingVetSpendingPolicy.GetHeadroomAsync(stayId, catId)</c>; above headroom → holds the
/// treatment pending approval."
///
/// Until HO-BRD-001 lands, <c>Fakes/Boarding/InMemoryBoardingVetSpendingPolicy</c> is fail-closed:
/// it reports zero headroom (<c>VetVisitAuthorised = false</c>, every amount zero) so a consumer
/// always falls back to the approval workflow rather than silently allowing an unbounded clinical
/// charge against a stay that may not even exist yet.
/// </summary>
public interface IBoardingVetSpendingPolicy
{
    Task<VetSpendingHeadroomResult> GetHeadroomAsync(Guid stayId, Guid catId, CancellationToken ct);
}
