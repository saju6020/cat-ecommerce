using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class StaffSessionEndedEvent : Event
{
    public required Guid SessionId { get; init; }
    public required Guid StaffAccountId { get; init; }
    public required SessionEndReason Reason { get; init; }
}
