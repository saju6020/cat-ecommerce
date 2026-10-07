using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Fakes.Boarding;

/// <summary>
/// FAKE until HO-BRD-001 lands -- replace this registration.
///
/// Fail-closed: both methods return <c>false</c> -- no stay or deposit reference can be confirmed
/// to exist, so a consumer cannot record a payment or issue food against a boarding reference that
/// might not be real.
/// </summary>
public sealed class InMemoryBoardingStayReferenceReader : IBoardingStayReferenceReader
{
    public Task<bool> ExistsForCustomer(BoardingPaymentReferenceKind kind, Guid id, Guid customerId, CancellationToken ct) =>
        Task.FromResult(false);

    public Task<bool> IsInHouse(Guid stayId, Guid catId, CancellationToken ct) =>
        Task.FromResult(false);
}
