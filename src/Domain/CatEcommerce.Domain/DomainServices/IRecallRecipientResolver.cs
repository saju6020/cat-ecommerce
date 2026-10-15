using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-IMP-001 §8.12 -- the distinct customers to notify of a recall: purchasers (resolved through merges) plus the CURRENT owners of
/// recorded cats, excluding anonymised and merged-away ids and those already notified (<c>recall:{batchId}:{customerId}</c>).
/// Read-only.
/// </summary>
public interface IRecallRecipientResolver
{
    Task<IReadOnlyList<RecallRecipient>> ResolveAsync(Guid batchId, CancellationToken ct);

    /// <summary>How many distinct recipients were already notified for this batch (for <c>alreadyNotified</c>).</summary>
    Task<int> CountAlreadyNotifiedAsync(Guid batchId, CancellationToken ct);
}
