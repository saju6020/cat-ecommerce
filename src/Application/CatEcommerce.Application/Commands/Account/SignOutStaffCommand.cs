using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

public sealed class SignOutStaffCommand : Command
{
    public required string TokenHash { get; init; }
}
