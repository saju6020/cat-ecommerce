using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Surgery;

/// <summary>HO-SRG-001 §11.1 C2 (US-SRG-01). Ids are minted in the controller; <c>BranchId</c> from the caller's active branch (HO-ADM-001 §8.2), never the body.
/// The campaign fields are phase G's.</summary>
public sealed class BookSurgeryCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid AppointmentId { get; init; }
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid ServiceId { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public required Guid TheatreId { get; init; }
    public required Guid SurgeonId { get; init; }
    public required Guid AssistantId { get; init; }
    public Guid? CampaignId { get; init; }
    public string? CampaignEligibilityNote { get; init; }

    /// <summary>The cat is recorded Neutered but the vet judges the record wrong: book the sterilisation anyway.</summary>
    public bool ConfirmNeuterStatusDiscrepancy { get; init; }
}

/// <summary>C3 -- <c>ExpectedVersion</c> guards a stale day list (<c>409</c>).</summary>
public sealed class RescheduleSurgeryCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public required Guid TheatreId { get; init; }
    public required Guid SurgeonId { get; init; }
    public required Guid AssistantId { get; init; }
}

/// <summary>C4.</summary>
public sealed class CancelSurgeryCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>C5 -- a nurse or vet completes a manual item; <c>WeightRecordId</c> is verified against the cat and the weight window.</summary>
public sealed class CompleteChecklistItemCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Kind { get; init; }
    public string? Note { get; init; }
    public Guid? WeightRecordId { get; init; }
}

public sealed class WaiveChecklistItemCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Kind { get; init; }
    public required string Reason { get; init; }
}

public sealed class RecordAsaGradeCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Grade { get; init; }
    public bool IsEmergency { get; init; }
}

/// <summary>C6.</summary>
public sealed class AdmitSurgeryCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>C7 -- without <c>ConsentOverrideReason</c> it is the ordinary start; with one it is the vet-only emergency override.</summary>
public sealed class StartSurgeryCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public string? ConsentOverrideReason { get; init; }
}
