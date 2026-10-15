using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Prescription;

/// <summary>
/// HO-PHM-001 §8.9 (ADR-145) -- the seal of one register entry in the per-branch SHA-256 hash chain. <c>Id</c> equals the entry's id.
/// <c>EntryHash = SHA-256(PreviousHash || canonical form of the entry)</c>; position 1 of a branch has the all-zero previous hash. Append-only like the
/// entries: no method changes a seal, the EF interceptor refuses one, and the database refuses UPDATE, DELETE and TRUNCATE. Written only by the
/// worker's sealing pass (<c>SealControlledDrugEntriesCommand</c>); a change made AROUND the three layers breaks the chain from that entry on, and the
/// verification query reports the first broken position.
/// </summary>
public sealed class ControlledDrugRegisterSealAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public long ChainPosition { get; private set; }
    public string PreviousHash { get; private set; } = default!;
    public string EntryHash { get; private set; } = default!;
    public DateTimeOffset SealedOn { get; private set; }

    private ControlledDrugRegisterSealAggregate() { }

    private ControlledDrugRegisterSealAggregate(Guid id) : base(id) { }

    /// <summary>Seals the entry: computes its hash from <paramref name="previousHash"/> and the entry's canonical form.</summary>
    public static ControlledDrugRegisterSealAggregate Seal(
        ControlledDrugCanonicalFields entry, long chainPosition, string previousHash, DateTimeOffset sealedOn, PharmacyActor actor)
    {
        if (chainPosition < 1)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidRegisterEntry, "A chain position starts at one.", nameof(chainPosition));

        if (previousHash.Length != 64)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidRegisterEntry, "A previous hash is 64 hexadecimal characters.", nameof(previousHash));

        var seal = new ControlledDrugRegisterSealAggregate(entry.Id)
        {
            BranchId = entry.BranchId,
            ChainPosition = chainPosition,
            PreviousHash = previousHash,
            EntryHash = ControlledDrugEntryCanonicalForm.Hash(previousHash, entry),
            SealedOn = sealedOn
        };

        seal.SetDefaultValue(actor.UserContext);
        seal.UpdateVersion();
        return seal;
    }
}
