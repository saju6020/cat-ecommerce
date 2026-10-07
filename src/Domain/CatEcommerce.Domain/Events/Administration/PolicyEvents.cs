using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Administration;

public sealed class PolicyDocumentCreatedEvent : Event
{
    public required Guid PolicyDocumentId { get; init; }
}

public sealed class PolicyVersionPublishedEvent : Event
{
    public required string PolicyKey { get; init; } = default!;
    public required int VersionNumber { get; init; }
    public required bool RequiresAcceptance { get; init; }
}

public sealed class PolicyVersionAcceptedEvent : Event
{
    public required Guid CustomerAccountId { get; init; }
    public required string PolicyKey { get; init; } = default!;
    public required int VersionNumber { get; init; }
}

/// <summary>HO-XCT-001 §5.3 Amendment / §8.8 -- raised by <c>PolicyAcceptanceAggregate.RedactIpAddress</c>.</summary>
public sealed class PolicyAcceptanceIpRedactedEvent : Event
{
    public required Guid PolicyAcceptanceId { get; init; }
}
