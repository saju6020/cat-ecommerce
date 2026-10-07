using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ACC-001 §8.10 / ADR-025 / ADR-027. Argon2id behind this port -- the aggregate never sees a
/// plaintext password (§20.2).
/// </summary>
public interface IPasswordHasher
{
    PasswordCredential Hash(string plaintext);

    bool Verify(string plaintext, PasswordCredential credential);

    /// <summary>Burns the same work as <see cref="Verify"/> for an unknown identifier, so response
    /// time does not disclose whether an account exists (ADR-028).</summary>
    void VerifyAgainstDummy(string plaintext);
}
