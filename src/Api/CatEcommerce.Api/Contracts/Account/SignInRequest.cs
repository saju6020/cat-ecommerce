namespace CatEcommerce.Api.Contracts.Account;

public sealed class SignInRequest
{
    public required string Identifier { get; init; }

    // SECURITY: plaintext. Never logged, never persisted, never echoed.
    public required string Password { get; init; }
}
