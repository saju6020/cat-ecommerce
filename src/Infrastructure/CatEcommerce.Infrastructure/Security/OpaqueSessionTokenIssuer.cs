using System.Security.Cryptography;
using System.Text;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Infrastructure.Security;

/// <summary>HO-ACC-001 §12.2 / ADR-031. 32 bytes CSPRNG, Base64Url; hash = hex SHA-256.</summary>
public sealed class OpaqueSessionTokenIssuer : ISessionTokenIssuer
{
    public SessionTokenMaterial Issue()
    {
        var token = Base64Url.Encode(RandomNumberGenerator.GetBytes(32));
        return new SessionTokenMaterial(token, HashToken(token));
    }

    public string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>Shared by session and reset-token generators -- no padding, URL-safe alphabet.</summary>
internal static class Base64Url
{
    public static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
