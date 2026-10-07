using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

public sealed class CreateStaffAccountCommand : Command
{
    public required Guid StaffAccountId { get; init; }
    public required string FullName { get; init; }
    public string? MobileNumber { get; init; }
    public string? EmailAddress { get; init; }
    public required IReadOnlyList<string> RoleIds { get; init; }
}
