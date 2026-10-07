using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-ACC-001 §8.10 / ADR-034. 256 CSPRNG bits, persisted as SHA-256.</summary>
public interface IPasswordResetTokenGenerator
{
    PasswordResetTokenMaterial Generate(DateTimeOffset asOf);

    string ComputeHash(string plainToken);
}
