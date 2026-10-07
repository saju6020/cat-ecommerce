namespace CatEcommerce.Domain.Models;

/// <summary>HO-ACC-001 §8.10. <see cref="Token"/> is the plaintext -- exists only in the handler's
/// stack frame and the dispatch request (§20.2).</summary>
public sealed record PasswordResetTokenMaterial(string Token, string TokenHash, DateTimeOffset ExpiresOn);
