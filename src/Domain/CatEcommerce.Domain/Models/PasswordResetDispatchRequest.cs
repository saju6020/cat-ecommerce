using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-ACC-001 §8.10 / §11.4 (C4). <see cref="PlainToken"/> is the plaintext reset link token --
/// exists only in the handler's stack frame and this request (§20.2). Not persisted anywhere:
/// only <c>PasswordResetTokenAggregate.TokenHash</c> is stored.
/// </summary>
public sealed record PasswordResetDispatchRequest(
    AccountKind AccountKind,
    Guid AccountId,
    PasswordResetChannel Channel,
    string MaskedDestination,
    string PlainToken,
    DateTimeOffset ExpiresOn);
