using System.Collections.Concurrent;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Infrastructure.Fakes.Billing;

/// <summary>
/// FAKE until HO-BIL-001 lands -- replace this registration.
///
/// Trivial in-memory recorder, not fail-closed: keeps recorded charges in a process-local
/// dictionary keyed by <see cref="ChargeSourceKey"/> so a consumer's idempotency assumption ("the
/// existing one if this source key was already recorded", HO-BIL-001 §8.4) can be exercised in
/// tests. It cannot truly enlist into the shared DbContext -- no <c>ChargeAggregate</c>/table
/// exists yet, and this contracts pass adds no entities.
/// </summary>
public sealed class InMemoryBillableChargeRecorder : IBillableChargeRecorder
{
    private readonly ConcurrentDictionary<(string SourceKind, Guid SourceId, string SourceLineRef), Guid> _recorded = new();

    public Task<Guid> RecordAsync(ChargeRequestDto request, CancellationToken ct = default)
    {
        var key = Key(request.SourceKind.DisplayName, request.SourceId, request.SourceLineRef);
        var id = _recorded.GetOrAdd(key, _ => Guid.NewGuid());
        return Task.FromResult(id);
    }

    public Task VoidAsync(ChargeSourceKey key, string reason, CancellationToken ct = default)
    {
        _recorded.TryRemove(Key(key.SourceKind.DisplayName, key.SourceId, key.SourceLineRef), out _);
        return Task.CompletedTask;
    }

    private static (string, Guid, string) Key(string sourceKind, Guid sourceId, string sourceLineRef) =>
        (sourceKind, sourceId, sourceLineRef);
}
