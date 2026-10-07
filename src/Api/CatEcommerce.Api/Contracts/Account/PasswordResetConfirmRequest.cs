namespace CatEcommerce.Api.Contracts.Account;

public sealed class PasswordResetConfirmRequest
{
    // SECURITY: plaintext. Never logged, never persisted, never echoed.
    public required string Token { get; init; }

    // SECURITY: plaintext. Never logged, never persisted, never echoed.
    public required string NewPassword { get; init; }
}
