namespace CatEcommerce.Api.Contracts.Surgery;

/// <summary>HO-SRG-001 §12.4 request bodies. The branch always comes from the caller's active branch (<c>X-Branch-Id</c>), never from a body.</summary>
public sealed class BookSurgeryRequest
{
    public required Guid CustomerId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid ServiceId { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public required Guid TheatreId { get; init; }
    public required Guid SurgeonId { get; init; }
    public required Guid AssistantId { get; init; }
    public Guid? CampaignId { get; init; }
    public string? CampaignEligibilityNote { get; init; }
    public bool ConfirmNeuterStatusDiscrepancy { get; init; }
}

public sealed class RescheduleSurgeryRequest
{
    public required int ExpectedVersion { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public required Guid TheatreId { get; init; }
    public required Guid SurgeonId { get; init; }
    public required Guid AssistantId { get; init; }
}

public sealed class CancelSurgeryRequest
{
    public required string Reason { get; init; }
}

public sealed class CompleteChecklistItemRequest
{
    public string? Note { get; init; }
    public Guid? WeightRecordId { get; init; }
}

public sealed class WaiveChecklistItemRequest
{
    public required string Reason { get; init; }
}

public sealed class RecordAsaGradeRequest
{
    public required string Grade { get; init; }
    public bool IsEmergency { get; init; }
}

public sealed class EndSurgicalProcedureRequest
{
    public required string Outcome { get; init; }
}

public sealed class StartWithConsentOverrideRequest
{
    public required string Reason { get; init; }
}

public sealed class SetRecoveryStatusRequest
{
    public required string Status { get; init; }
    public string? Note { get; init; }
}

public sealed class DischargeFollowUpRequest
{
    public required string Kind { get; init; }
    public required DateOnly DueOn { get; init; }
}

/// <summary>Both fields are optional: omitted instructions mean the protocol one, an omitted follow-up list means the protocol defaults, an empty list means none.</summary>
public sealed class DischargeSurgeryRequest
{
    public string? PostOpInstructions { get; init; }
    public IReadOnlyList<DischargeFollowUpRequest>? FollowUps { get; init; }
}

public sealed class CancelSurgeryFollowUpRequest
{
    public required string Reason { get; init; }
}

public sealed class SurgeryCampaignPriceRequest
{
    public required Guid ServiceId { get; init; }
    public required decimal Amount { get; init; }
}

public sealed class CreateSurgeryCampaignRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required DateOnly ValidFrom { get; init; }
    public required DateOnly ValidTo { get; init; }
    public IReadOnlyList<Guid>? BranchIds { get; init; }
    public required IReadOnlyList<SurgeryCampaignPriceRequest> Prices { get; init; }
    public int? MaxCases { get; init; }
}

public sealed class UpdateSurgeryCampaignRequest
{
    public required int ExpectedVersion { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required DateOnly ValidFrom { get; init; }
    public required DateOnly ValidTo { get; init; }
    public IReadOnlyList<Guid>? BranchIds { get; init; }
    public required IReadOnlyList<SurgeryCampaignPriceRequest> Prices { get; init; }
    public int? MaxCases { get; init; }
}

public sealed class SetSurgeryCampaignActiveRequest
{
    public required bool IsActive { get; init; }
}
