using CatEcommerce.Api.Contracts;

namespace CatEcommerce.Api.Contracts.Account;

public sealed class RegisterCustomerRequest : ICaptchaProtectedRequest
{
    public required string FullName { get; init; }
    public string? MobileNumber { get; init; }
    public string? EmailAddress { get; init; }

    // SECURITY: plaintext. Never logged, never persisted, never echoed.
    public required string Password { get; init; }

    public string? CaptchaId { get; init; }
    public string? CaptchaAnswer { get; init; }
}
