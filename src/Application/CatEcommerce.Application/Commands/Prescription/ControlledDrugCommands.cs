using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Prescription;

// HO-PHM-001 §11.1 C7, C8. A witness password is on a command ONLY because dispatch is in-process (.ai/cqrs.md -- the same reasoning as HO-ACC-001's sign-in):
// it never reaches an event, a log, audit capture or the notification outbox, and every command that carries it overrides ToString() to omit it (U-X2).

/// <summary>C7 -- a correction to the controlled-drug register: a NEW entry linked to the one it corrects (the original is never touched, ADR-144). Vet alone
/// (Appendix D, OQ-8); a verified witness is required; a non-zero quantity also enlists an Inventory adjustment in the same save.</summary>
public sealed class RecordControlledDrugCorrectionCommand : Command
{
    /// <summary>The new entry's id, minted in the controller.</summary>
    public required Guid EntryId { get; init; }
    public required Guid CorrectsEntryId { get; init; }
    public string? CorrectedDoseText { get; init; }
    public Guid? CorrectedCatId { get; init; }
    public required decimal QuantityDelta { get; init; }
    public string? Notes { get; init; }
    public required string Reason { get; init; }
    public required string WitnessIdentifier { get; init; }
    public required string WitnessPassword { get; init; }

    /// <summary>The caller's active branch (an entry of another branch is a mismatch).</summary>
    public required Guid BranchId { get; init; }

    public override string ToString() => $"{nameof(RecordControlledDrugCorrectionCommand)} {{ EntryId = {EntryId}, CorrectsEntryId = {CorrectsEntryId}, BranchId = {BranchId} }}";
}

/// <summary>C8 -- the worker's sealing pass for one branch (ADR-145): seals unsealed entries older than the settle delay, in entry order, at most 500 per pass.</summary>
public sealed class SealControlledDrugEntriesCommand : Command
{
    public required Guid BranchId { get; init; }
}

/// <summary>Audits an export of the register for inspection (a query never writes, so the audit row is this command's).</summary>
public sealed class RecordControlledDrugRegisterExportCommand : Command
{
    public required Guid BranchId { get; init; }
    public Guid? MedicineId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public required int RowCount { get; init; }
}

/// <summary>The answer to a successful correction.</summary>
public sealed record RecordedRegisterEntry(Guid EntryId, long EntryNumber);

/// <summary>The answer to a sealing pass.</summary>
public sealed record SealingResult(int Sealed, bool LostRace);
