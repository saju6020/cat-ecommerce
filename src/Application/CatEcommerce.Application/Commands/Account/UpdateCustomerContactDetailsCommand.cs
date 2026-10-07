using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

public sealed class UpdateCustomerContactDetailsCommand : Command
{
    public required string FullName { get; init; }
    public string? EmailAddress { get; init; }
    public required IReadOnlyList<string> AdditionalPhoneNumbers { get; init; }

    // SECURITY: plaintext. Never logged, never persisted, never echoed. Required only when
    // EmailAddress is actually changing (SECURITY gate LOW, HO-ACC-001 §1 Amended -- the same
    // step-up C18 already requires, because email is also a login and reset identifier).
    public string? CurrentPassword { get; init; }
}
