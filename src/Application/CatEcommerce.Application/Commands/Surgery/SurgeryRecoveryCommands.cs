using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Surgery;

/// <summary>C14 (US-SRG-10) -- change the recovery status of a surgery that is in Recovery. A change to the same status is a no-op.</summary>
public sealed class SetRecoveryStatusCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Status { get; init; }
    public string? Note { get; init; }
}

public sealed record DischargeFollowUpInput(string Kind, DateOnly DueOn);

/// <summary>
/// C15 (US-SRG-07) -- discharge. <c>PostOpInstructions</c> null means "the protocol's"; <c>FollowUps</c> null means "the protocol's defaults", an empty list means none (for example
/// absorbable sutures). A date outside the protocol window is <c>FollowUpOutsideWindow</c>.
/// </summary>
public sealed class DischargeSurgeryCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public string? PostOpInstructions { get; init; }
    public IReadOnlyList<DischargeFollowUpInput>? FollowUps { get; init; }
}

public sealed class CompleteSurgeryFollowUpCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid FollowUpId { get; init; }
}

public sealed class CancelSurgeryFollowUpCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid FollowUpId { get; init; }
    public required string Reason { get; init; }
}
