using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

/// <summary>Owner, 2026-10-20: an administrator edits a staff member's name, mobile number and email address.</summary>
public sealed class UpdateStaffAccountDetailsCommand : Command
{
    public required Guid StaffAccountId { get; init; }
    public required string FullName { get; init; }
    public string? MobileNumber { get; init; }
    public string? EmailAddress { get; init; }
}
