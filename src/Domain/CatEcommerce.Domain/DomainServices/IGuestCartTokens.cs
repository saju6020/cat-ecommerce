namespace CatEcommerce.Domain.DomainServices;

/// <summary>A freshly minted guest-cart token (the cookie value) and the SHA-256 hex hash that is the only thing stored.</summary>
public sealed record GuestCartToken(string Token, string Hash);

/// <summary>
/// HO-ORD-001 §8.14 / ADR-199 -- mints and hashes the opaque guest-cart token: 32 random bytes from the system CSPRNG, base64url in the
/// <c>pc_cart</c> cookie, SHA-256 (64 hex characters) in the database. The token is a bearer secret; it is never stored, logged or returned in a body.
/// Implemented in Infrastructure.
/// </summary>
public interface IGuestCartTokens
{
    GuestCartToken Mint();

    /// <summary>The hash of a cookie value, or <c>null</c> when the value is not a well-formed token (a forged cookie finds no cart, never an error).</summary>
    string? HashOrNull(string? token);
}
