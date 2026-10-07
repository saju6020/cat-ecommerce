using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

public sealed class ResetPasswordCommand : Command
{
    // SECURITY: plaintext. Never logged, never persisted, never echoed.
    public required string Token { get; init; }

    // SECURITY: plaintext. Never logged, never persisted, never echoed.
    public required string NewPassword { get; init; }
}
