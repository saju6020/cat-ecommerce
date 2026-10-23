using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

/// <summary>Owner, 2026-10-20: an administrator changed a staff member's name, mobile number or email address.</summary>
public sealed class StaffAccountDetailsChangedEvent : Event
{
    public required Guid StaffAccountId { get; init; }
    public required string PreviousFullName { get; init; }
    public string? PreviousMobileNumber { get; init; }
    public string? PreviousEmailAddress { get; init; }
    public required string NewFullName { get; init; }
    public string? NewMobileNumber { get; init; }
    public string? NewEmailAddress { get; init; }
}
