using CatEcommerce.Domain.DomainServices;

namespace CatEcommerce.Application.Tests.TestSupport;

/// <summary>
/// Hand-written in-memory test double for <see cref="IStaleHoldReleaser"/> -- no mocking library
/// is in the allowed scope for this slice (HO-APT-001 §13), the same precedent
/// <see cref="FakeAggregateRootRepository{TAggregate}"/> already uses.
/// </summary>
public sealed class FakeStaleHoldReleaser : IStaleHoldReleaser
{
    /// <summary>What <see cref="FindStaleOverlappingHoldIdsAsync"/> returns; empty by default.</summary>
    public IReadOnlyList<Guid> StaleIds { get; set; } = [];

    /// <summary>What <see cref="HasPendingChanges"/> returns -- ADR-070's 2026-09-28 amendment's
    /// guard input.</summary>
    public bool PendingChanges { get; set; }

    public Task<IReadOnlyList<Guid>> FindStaleOverlappingHoldIdsAsync(
        DateTimeOffset start, DateTimeOffset end, Guid? performerId, Guid? roomId, Guid catId,
        DateTimeOffset now, Guid? excludeAppointmentId, CancellationToken ct = default) =>
        Task.FromResult(StaleIds);

    public Task<Guid?> FindActiveHoldIdForCustomerAsync(Guid customerId, Guid excludeAppointmentId, CancellationToken ct = default) =>
        Task.FromResult((Guid?)null);

    public bool HasPendingChanges() => PendingChanges;

    /// <summary>When true, <see cref="TryCommitExpiryAsync"/> behaves as if another request changed the
    /// hold first: the commit is not run and <c>false</c> is returned.</summary>
    public bool LoseTheRace { get; set; }

    public Task<bool> TryCommitExpiryAsync(Platform.Infrastructure.Core.Domain.AggregateRoot stale, Func<Task> commit)
    {
        if (LoseTheRace) return Task.FromResult(false);
        return CommitAsync(commit);
    }

    private static async Task<bool> CommitAsync(Func<Task> commit)
    {
        await commit();
        return true;
    }
}
