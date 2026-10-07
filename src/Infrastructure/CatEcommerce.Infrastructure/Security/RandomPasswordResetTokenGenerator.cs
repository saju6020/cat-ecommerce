using System.Security.Cryptography;
using System.Text;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Security;

/// <summary>HO-ACC-001 §12.2 / ADR-034. 32 bytes CSPRNG; Base64Url; SHA-256 hash; lifetime from options.</summary>
public sealed class RandomPasswordResetTokenGenerator(IOptions<AccountOptions> options) : IPasswordResetTokenGenerator
{
    public PasswordResetTokenMaterial Generate(DateTimeOffset asOf)
    {
        var token = Base64Url.Encode(RandomNumberGenerator.GetBytes(32));
        var expiresOn = asOf.AddMinutes(options.Value.PasswordResetTokenLifetimeMinutes);

        return new PasswordResetTokenMaterial(token, ComputeHash(token), expiresOn);
    }

    public string ComputeHash(string plainToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plainToken)));
}
