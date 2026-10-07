namespace CatEcommerce.Api.Contracts.Account;

public sealed class DeleteAccountRequest
{
    // SECURITY: plaintext. Never logged, never persisted, never echoed.
    public required string CurrentPassword { get; init; }
}
