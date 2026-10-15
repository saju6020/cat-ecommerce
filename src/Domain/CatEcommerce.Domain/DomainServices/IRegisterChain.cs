using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>The newest seal of a branch's chain: its position and hash.</summary>
public sealed record RegisterSealTip(long ChainPosition, string EntryHash);

/// <summary>
/// HO-PHM-001 §8.9 / §12.2 (ADR-145) -- the infrastructure half of the register's hash chain, used by <c>SealControlledDrugEntriesCommand</c> (the worker's
/// sealing pass). It reads the entries as the primitives their seal is hashed over and appends seals; the hashing itself is the pure
/// <see cref="ControlledDrugEntryCanonicalForm"/>. Nothing here ever updates or deletes a row.
/// </summary>
public interface IRegisterChain
{
    /// <summary>The branch's newest seal, or <c>null</c> when nothing has been sealed yet (the next position is 1 with the all-zero previous hash).</summary>
    Task<RegisterSealTip?> GetTipAsync(Guid branchId, CancellationToken ct);

    /// <summary>Entries of the branch with no seal and <c>RecordedOn &lt; recordedBefore</c>, in <c>EntryNumber</c> order, at most <paramref name="take"/> -- the settle
    /// delay keeps an in-flight transaction with a lower number from being skipped for long.</summary>
    Task<IReadOnlyList<ControlledDrugCanonicalFields>> GetUnsealedAsync(Guid branchId, DateTimeOffset recordedBefore, int take, CancellationToken ct);

    /// <summary>Appends the seals in ONE save. Returns <c>false</c> when another worker already sealed that position (a <c>23505</c> on the chain index or the primary key): the pass
    /// stops and retries next time, and nothing is written.</summary>
    Task<bool> TryAppendAsync(IReadOnlyList<ControlledDrugRegisterSealAggregate> seals, CancellationToken ct);
}
