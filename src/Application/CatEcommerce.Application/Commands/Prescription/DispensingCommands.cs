using CatEcommerce.Domain.ValueObjects;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Prescription;

// HO-PHM-001 §11.1 C9-C11. A witness password is on a command ONLY because dispatch is in-process (.ai/cqrs.md); it never reaches an event, a log, audit capture or the
// outbox, and every command that carries it overrides ToString() to omit it (U-X2).

public sealed record DispenseLineModel(Guid SkuId, Guid BatchId, decimal Quantity);

/// <summary>
/// C9 -- dispense one supply of a prescription (US-PHM-05, US-PHM-04 AC2, US-PHM-06 AC1). The lines name the EXACT batches handed over (the server suggests FEFO; a different
/// batch needs <see cref="FefoOverrideReason"/>, BR-4); an expired batch is refused with no override (BR-3). A controlled medicine needs a witness who authenticates with their
/// own identifier and password in this same request (ADR-146). The branch is the caller's active branch.
/// </summary>
public sealed class DispensePrescriptionCommand : Command
{
    public required Guid DispensingId { get; init; }
    public required Guid PrescriptionId { get; init; }
    public required IReadOnlyList<DispenseLineModel> Lines { get; init; }
    public string? FefoOverrideReason { get; init; }
    public Guid? RepeatRequestId { get; init; }
    public string? WitnessIdentifier { get; init; }
    public string? WitnessPassword { get; init; }
    public required Guid BranchId { get; init; }

    public override string ToString() => $"{nameof(DispensePrescriptionCommand)} {{ DispensingId = {DispensingId}, PrescriptionId = {PrescriptionId}, BranchId = {BranchId} }}";
}

/// <summary>C10 -- a label print (the first print counts); the print itself is the browser's (ADR-147).</summary>
public sealed class RecordLabelPrintCommand : Command
{
    public required Guid DispensingId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>C11 -- reverse the latest supply within the reversal window (Vet, Administrator). A controlled medicine needs a verified witness.</summary>
public sealed class ReverseDispensingCommand : Command
{
    public required Guid DispensingId { get; init; }
    public required string Reason { get; init; }
    public string? WitnessIdentifier { get; init; }
    public string? WitnessPassword { get; init; }
    public required Guid BranchId { get; init; }

    public override string ToString() => $"{nameof(ReverseDispensingCommand)} {{ DispensingId = {DispensingId}, BranchId = {BranchId} }}";
}

/// <summary>The answer to a successful dispense: the supply number, the frozen label and the lines that had no price (OQ-6: care is not blocked by a missing price).</summary>
public sealed record DispensedResult(Guid DispensingId, int SupplyNumber, MedicineLabelSnapshot Label, IReadOnlyList<Guid> UnpricedLineIds);
