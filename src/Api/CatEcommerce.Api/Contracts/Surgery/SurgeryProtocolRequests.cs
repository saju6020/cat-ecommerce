namespace CatEcommerce.Api.Contracts.Surgery;

/// <summary>HO-SRG-001 §12.4 request bodies -- protocols.</summary>
public sealed class UpsertSurgeryProtocolRequest
{
    public required string FastingInstructions { get; init; }
    public required int FastingHours { get; init; }
    public required string PostOpInstructions { get; init; }
    public IReadOnlyList<SurgeryProtocolFollowUpRequest> FollowUps { get; init; } = [];
}

public sealed class SurgeryProtocolFollowUpRequest
{
    public required string Kind { get; init; }
    public required int DayOffset { get; init; }
    public required int WindowFromDay { get; init; }
    public required int WindowToDay { get; init; }
}
