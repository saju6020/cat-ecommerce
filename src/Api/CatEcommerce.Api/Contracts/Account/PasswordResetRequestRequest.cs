namespace CatEcommerce.Api.Contracts.Account;

public sealed class PasswordResetRequestRequest
{
    public required string Identifier { get; init; }
}
