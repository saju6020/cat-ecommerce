using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Owned by HO-BRD-001 §12.8 P2, consumed by HO-BIL-001. "Boarding implements
/// <c>IBoardingBillableSource.GetUninvoicedLinesAsync(stayId, upToNight?)</c> → lines...; Billing
/// adds clinic charges carrying <c>BoardingStayId</c> to the same invoice, applies deposit payments
/// and turns negative lines into a credit note against the invoice that billed them (BR-11)."
///
/// Until HO-BRD-001 lands, <c>Fakes/Boarding/InMemoryBoardingBillableSource</c> always returns an
/// empty line list -- there is no stay to bill yet, so this is the only truthful answer (not a
/// business-rule denial, simply nothing exists).
/// </summary>
public interface IBoardingBillableSource
{
    Task<IReadOnlyList<BoardingUninvoicedLineDto>> GetUninvoicedLinesAsync(Guid stayId, int? upToNight, CancellationToken ct);
}
