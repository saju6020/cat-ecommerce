using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// A clinical or financial context's own count of records past their retention period
/// (HO-XCT-001 §8.10, §5.2 C-ALL-3). Registered by the owning area in its own module: EMR, PHM,
/// SRG, BIL and IMP each provide one. Clinical: <c>retention.clinicalYears</c> after the cat's
/// last visit or death (D-8). Financial: <c>retention.financialYears</c> after the document date
/// (D-9). The end of a period is a review flag only -- **never** an automatic delete or
/// anonymisation (ADR-301).
/// </summary>
public interface IRetentionReviewSource
{
    /// <summary>Stable identifier for this source, e.g. "cat-clinical".</summary>
    string SourceKey { get; }

    Task<IReadOnlyList<RetentionReviewCount>> CountPastRetentionAsync(DateOnly asOf, CancellationToken ct);
}
