namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-APT-001 §8.6/§8.7 (ADR-070) -- finds <c>Held</c> appointments whose <c>HoldExpiresOn</c> has
/// passed, so every write that takes time can expire them (each via
/// <c>AppointmentAggregate.ExpireHold</c>) before it enlists its own row. Not named in §8.12's own
/// port table -- added because <c>IAggregateRootRepository&lt;T&gt;</c>'s generic contract
/// (<c>GetByIdAsync</c>/<c>GetByFilterAsync</c>/<c>ExistAsync</c>) has no "find every matching row"
/// method, and this needs one to find every stale hold, not just the first.
///
/// ADR-070's 2026-09-28 amendment: each stale hold's expiry now commits in its own save
/// (<c>SlotClaim.ExpireOverlappingStaleHoldsAsync</c>), before the caller's own claim, not "in the
/// same save" -- see that method and the ADR.
/// </summary>
public interface IStaleHoldReleaser
{
    /// <summary>Every <c>Held</c> row, of any branch, whose <c>HoldExpiresOn</c> is at or before
    /// <paramref name="now"/> and which overlaps the given performer, room or cat -- ordered by id, so
    /// two requests expiring the same stale holds always lock them in the same order and cannot
    /// deadlock each other (backlog #31).</summary>
    Task<IReadOnlyList<Guid>> FindStaleOverlappingHoldIdsAsync(
        DateTimeOffset start, DateTimeOffset end, Guid? performerId, Guid? roomId, Guid catId,
        DateTimeOffset now, Guid? excludeAppointmentId, CancellationToken ct = default);

    /// <summary>Runs <paramref name="commit"/> -- the save of <paramref name="stale"/>'s expiry -- and
    /// reports whether it went through (backlog #31). <c>false</c> means another request changed that
    /// hold first (expired, confirmed or released it) while this one waited on its row lock, so the
    /// optimistic-concurrency check failed: the stale tracked copy is discarded and the caller moves
    /// on. If the hold was in fact confirmed, the caller's own claim then loses on the exclusion
    /// constraint and is reported as <c>SlotNoLongerAvailable</c> -- never the generic "changed by
    /// someone else" 409.</summary>
    Task<bool> TryCommitExpiryAsync(Platform.Infrastructure.Core.Domain.AggregateRoot stale, Func<Task> commit);

    /// <summary>The caller's own <c>Held</c> appointment, if any (ADR-070's one-hold-per-customer
    /// rule) -- stale or not; the handler releases it regardless of its own expiry.</summary>
    Task<Guid?> FindActiveHoldIdForCustomerAsync(Guid customerId, Guid excludeAppointmentId, CancellationToken ct = default);

    /// <summary>True if the underlying <c>DbContext</c> already has tracked, unsaved changes.
    /// <c>SlotClaim.ExpireOverlappingStaleHoldsAsync</c> asserts this is false on entry (ADR-070's
    /// 2026-09-28 amendment): since it now commits the stale holds' expiry in its own save, a
    /// caller with pending changes of its own would have half its unit of work committed early by
    /// that save.</summary>
    bool HasPendingChanges();
}
