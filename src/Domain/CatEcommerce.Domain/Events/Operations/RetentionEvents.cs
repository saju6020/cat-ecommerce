using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Operations;

// HO-XCT-001 section 11.5 -- ids, enumerations and counts only.

public sealed class RetentionRunRequestedEvent : Event
{
    public required Guid RetentionRunId { get; init; }
    public required RetentionRunTrigger Trigger { get; init; }
}

public sealed class RetentionRunStartedEvent : Event
{
    public required Guid RetentionRunId { get; init; }
}

public sealed class RetentionPassRecordedEvent : Event
{
    public required Guid RetentionRunId { get; init; }
    public required RetentionPassKind PassKind { get; init; }
    public required int Affected { get; init; }
    public required int Failed { get; init; }
}

/// <summary>Raised once at the end of a run; the audit handler writes the run one business-event summary from it.</summary>
public sealed class RetentionRunFinishedEvent : Event
{
    public required Guid RetentionRunId { get; init; }
    public required RetentionRunTrigger Trigger { get; init; }
    public required RetentionRunStatus RunStatus { get; init; }
    public required int PassCount { get; init; }
    public required int TotalAffected { get; init; }
    public required int TotalHeld { get; init; }
    public required int TotalFailed { get; init; }
}
