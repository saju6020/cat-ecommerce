using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Infrastructure.Fakes.Boarding;

/// <summary>
/// FAKE until HO-BRD-001 lands -- replace this registration.
///
/// Always returns an empty line list -- there is no stay to bill yet, so this is the only truthful
/// answer (not a business-rule denial, simply nothing exists).
/// </summary>
public sealed class InMemoryBoardingBillableSource : IBoardingBillableSource
{
    public Task<IReadOnlyList<BoardingUninvoicedLineDto>> GetUninvoicedLinesAsync(Guid stayId, int? upToNight, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<BoardingUninvoicedLineDto>>(Array.Empty<BoardingUninvoicedLineDto>());
}
