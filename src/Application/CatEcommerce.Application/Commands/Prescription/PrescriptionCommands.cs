using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Prescription;

// HO-PHM-001 §11.1 C5, C6, C12-C16. Data only: ids are minted in the controller and enumerations travel as their display names.
// Every command is dispatched with ValidateCommand = true (F3).

public sealed record DoseModel(decimal Amount, string Unit);

/// <summary>
/// The fields a prescribing command carries -- shared by issue (C5) and renew (C15) so both run the SAME steps (<c>PrescribingSteps</c>). The
/// acknowledgement fields bind a vet's "I have seen these warnings" to exactly the warnings they saw (<see cref="AcknowledgedWarningsFingerprint"/>, ADR-139).
/// </summary>
public abstract class PrescribingFieldsCommandBase : Command
{
    public required DoseModel Dose { get; init; }
    public required string Route { get; init; }
    public required string Frequency { get; init; }
    public required int DurationDays { get; init; }
    public required decimal Quantity { get; init; }
    public required string Instructions { get; init; }

    /// <summary>Defaults to today, branch-local; not before yesterday.</summary>
    public DateOnly? StartsOn { get; init; }
    public bool IsRepeatable { get; init; }
    public int RepeatsAuthorised { get; init; }

    /// <summary>Required when repeatable; for a non-repeatable prescription it defaults to <c>StartsOn + pharmacy.defaultDispenseWindowDays</c>.</summary>
    public DateOnly? ValidUntil { get; init; }

    public string? AcknowledgedWarningsFingerprint { get; init; }
    public string? AllergyAcknowledgementReason { get; init; }
    public string? DoseAcknowledgementNote { get; init; }

    /// <summary>The caller's active branch (a visit of another branch is "not found").</summary>
    public required Guid BranchId { get; init; }
}

/// <summary>C5 -- a vet issues a prescription for a cat from an OPEN visit (US-PHM-01, 02, 03).</summary>
public sealed class IssuePrescriptionCommand : PrescribingFieldsCommandBase
{
    public required Guid PrescriptionId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid VisitId { get; init; }
    public required Guid MedicineId { get; init; }
}

/// <summary>C6 -- a vet cancels a prescription with a reason; an open repeat request is declined with the same reason and the owner told.</summary>
public sealed class CancelPrescriptionCommand : Command
{
    public required Guid PrescriptionId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>The answer to a successful issue or renew.</summary>
public sealed record IssuedPrescription(Guid PrescriptionId, string PrescriptionNumber);
