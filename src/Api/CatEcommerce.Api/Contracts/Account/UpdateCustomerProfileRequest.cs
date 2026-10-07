namespace CatEcommerce.Api.Contracts.Account;

public sealed class UpdateCustomerProfileRequest
{
    public required string FullName { get; init; }
    public string? EmailAddress { get; init; }
    public IReadOnlyList<string> AdditionalPhoneNumbers { get; init; } = [];

    // SECURITY gate LOW / HO-ACC-001 §1 Amended: required only when EmailAddress is actually
    // changing -- the handler is what decides that, since it alone knows the current value.
    public string? CurrentPassword { get; init; }
}
