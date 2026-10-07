using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-ACC-001 §8.10 / ADR-031. Opaque 256-bit session tokens, stored only as SHA-256 hashes.</summary>
public interface ISessionTokenIssuer
{
    SessionTokenMaterial Issue();

    string HashToken(string token);
}
