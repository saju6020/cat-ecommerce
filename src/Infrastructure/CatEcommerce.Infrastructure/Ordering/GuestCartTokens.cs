using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>HO-ORD-001 ADR-199 -- 32 random bytes (base64url) as the cookie value, SHA-256 hex as the stored hash. A malformed cookie value
/// hashes to nothing, so a forged or truncated cookie finds no cart.</summary>
public sealed class GuestCartTokens : IGuestCartTokens
{
    public GuestCartToken Mint()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(CartRules.TokenBytes));
        return new GuestCartToken(token, Hash(token));
    }

    public string? HashOrNull(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64) return null;

        try
        {
            return Base64Url.DecodeFromChars(token).Length == CartRules.TokenBytes ? Hash(token) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
