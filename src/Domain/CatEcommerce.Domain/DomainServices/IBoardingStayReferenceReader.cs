using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Owned by HO-BRD-001 §12.8 P1/I1, consumed by HO-BIL-001 (validates a payment reference) and
/// HO-IMP-001 (validates a boarding food issue). Method names taken from those two sections'
/// prose: "validates it with <c>IBoardingStayReferenceReader.ExistsForCustomer(kind, id,
/// customerId)</c>" (P1) and "validated with
/// <c>IBoardingStayReferenceReader.IsInHouse(stayId, catId)</c>" (I1).
///
/// Until HO-BRD-001 lands, <c>Fakes/Boarding/InMemoryBoardingStayReferenceReader</c> is
/// fail-closed: both methods return <c>false</c> -- no stay or deposit reference can be confirmed to
/// exist, so a consumer cannot record a payment or issue food against a boarding reference that
/// might not be real.
/// </summary>
public interface IBoardingStayReferenceReader
{
    Task<bool> ExistsForCustomer(BoardingPaymentReferenceKind kind, Guid id, Guid customerId, CancellationToken ct);

    Task<bool> IsInHouse(Guid stayId, Guid catId, CancellationToken ct);
}
