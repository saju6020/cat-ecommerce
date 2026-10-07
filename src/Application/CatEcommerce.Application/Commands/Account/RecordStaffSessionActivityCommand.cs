using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

public sealed class RecordStaffSessionActivityCommand : Command
{
    public required string TokenHash { get; init; }
    public string? IpAddress { get; init; }
}
