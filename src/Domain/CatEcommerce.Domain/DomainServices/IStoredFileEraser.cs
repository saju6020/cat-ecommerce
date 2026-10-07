namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-FIL-001 §8.7: "Purge is object first, row second -- a deleted row with a live object is an
/// orphan nobody will ever find; a deleted object with a live row is a visible, fixable error."
/// <c>IAggregateRootRepository&lt;T&gt;</c> has no hard-delete member (.ai/persistence-ef-core.md
/// §1) -- a <c>StoredFileAggregate</c> row must actually disappear (§8.1: "a file has no lifecycle
/// beyond existing and being deleted"), not merely be marked. This port is the one place that
/// performs the object-then-row sequence, shared by <c>DeleteStoredFileCommandHandler</c> and
/// <c>StoredFilePurgeSource</c> so the ordering is written once.
/// </summary>
public interface IStoredFileEraser
{
    /// <summary>Deletes the bucket object (idempotent -- a missing object is success, §8.4) then
    /// the row. Returns <c>false</c> if no row with <paramref name="fileId"/> exists (nothing to
    /// do); never throws for that case.</summary>
    Task<bool> EraseAsync(Guid fileId, CancellationToken ct);
}
