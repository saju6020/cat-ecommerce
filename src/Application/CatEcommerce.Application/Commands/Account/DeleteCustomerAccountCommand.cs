using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

public sealed class DeleteCustomerAccountCommand : Command
{
    // SECURITY: plaintext. Never logged, never persisted, never echoed.
    public required string CurrentPassword { get; init; }
}
