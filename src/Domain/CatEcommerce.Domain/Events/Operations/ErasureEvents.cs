using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Operations;

// HO-XCT-001 section 11.5 -- ids, enumerations, dates, codes and context keys only. The ledger holds no personal data and neither do its events.

public sealed class ErasureRequestOpenedEvent : Event
{
    public required Guid CustomerAccountId { get; init; }
    public required ErasureRequestSource RequestSource { get; init; }
}

public sealed class ErasureScopesAddedEvent : Event
{
    public required Guid CustomerAccountId { get; init; }
    public required IReadOnlyList<string> ContextKeys { get; init; }
}

public sealed class ErasureScopeCompletedEvent : Event
{
    public required Guid CustomerAccountId { get; init; }
    public required string ContextKey { get; init; }
}

public sealed class ErasureScopeHeldEvent : Event
{
    public required Guid CustomerAccountId { get; init; }
    public required string ContextKey { get; init; }
    public required DateOnly HeldUntil { get; init; }
}

public sealed class ErasureScopeFailedEvent : Event
{
    public required Guid CustomerAccountId { get; init; }
    public required string ContextKey { get; init; }
    public required string ErrorCode { get; init; }
}

/// <summary>The proof point for D-11: every context has erased everything it may erase, and everything it held has been released.</summary>
public sealed class ErasureRequestCompletedEvent : Event
{
    public required Guid CustomerAccountId { get; init; }
    public required DateTimeOffset CompletedOn { get; init; }
}
