namespace CatEcommerce.Domain.Models;

/// <summary>HO-ACC-001 §8.10. <see cref="Token"/> is the plaintext -- exists only in the handler's
/// stack frame and the cookie the controller writes (§20.2).</summary>
public sealed record SessionTokenMaterial(string Token, string TokenHash);
