using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

public sealed class SignInStaffCommand : Command
{
    public required Guid SessionId { get; init; }
    public required string Identifier { get; init; }

    // SECURITY: plaintext. Never logged, never persisted, never echoed.
    public required string Password { get; init; }

    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}
