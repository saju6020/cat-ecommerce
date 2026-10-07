using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Infrastructure.Fakes.Boarding;

/// <summary>
/// FAKE until HO-BRD-001 lands -- replace this registration.
///
/// Fail-closed: always reports zero headroom (<c>VetVisitAuthorised = false</c>, every amount
/// zero), so a consumer always falls back to the approval workflow rather than silently allowing an
/// unbounded clinical charge against a stay that may not even exist yet.
/// </summary>
public sealed class InMemoryBoardingVetSpendingPolicy : IBoardingVetSpendingPolicy
{
    public Task<VetSpendingHeadroomResult> GetHeadroomAsync(Guid stayId, Guid catId, CancellationToken ct) =>
        Task.FromResult(new VetSpendingHeadroomResult(
            VetVisitAuthorised: false,
            Limit: Money.Zero("BDT"),
            Committed: Money.Zero("BDT"),
            Headroom: Money.Zero("BDT")));
}
