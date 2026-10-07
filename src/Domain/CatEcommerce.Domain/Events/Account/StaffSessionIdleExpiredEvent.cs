using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class StaffSessionIdleExpiredEvent : Event
{
    public required Guid SessionId { get; init; }
    public required Guid StaffAccountId { get; init; }
    public required DateTimeOffset LastActivityOn { get; init; }
    public required DateTimeOffset ExpiredOn { get; init; }
    public required DateTimeOffset DetectedOn { get; init; }
    public required int IdleTimeoutMinutes { get; init; }
}
