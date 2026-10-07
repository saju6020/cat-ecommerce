using System.Security.Cryptography;
using System.Text;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Files;

/// <summary>
/// HO-FIL-001 §8.6 (inherited verbatim from HO-EMR-001 §8.10):
/// <c>/api/files/{fileId}/content?exp={unix}&amp;disp={inline|attachment}&amp;sig={base64url}</c>
/// where <c>sig = HMAC-SHA256(FileStorage:UrlSigningKey, "{fileId}|{exp}|{disp}")</c>. Registered
/// as both <see cref="IFileUrlSigner"/> (for consuming reads) and by its own concrete type (for
/// <c>FilesController</c>'s serving endpoint, which also needs <see cref="TryVerify"/> --
/// verification is this context's own serving-path concern, not part of the cross-area
/// <see cref="IFileUrlSigner"/> contract in docs/handover/CONTRACTS.md).
/// </summary>
public sealed class HmacFileUrlSigner(IOptions<FileStorageOptions> options, TimeProvider timeProvider) : IFileUrlSigner
{
    public string Sign(Guid fileId, TimeSpan expiresIn, string disposition)
    {
        var exp = timeProvider.GetUtcNow().Add(expiresIn).ToUnixTimeSeconds();
        var signature = ComputeSignature(fileId, exp, disposition);

        return $"/api/files/{fileId:D}/content?exp={exp}&disp={Uri.EscapeDataString(disposition)}&sig={Base64UrlEncode(signature)}";
    }

    /// <summary>Constant-time signature check (U8) -- refuses a tampered or expired URL with no
    /// detail (§8.6). Expiry is checked separately by the caller since it is not secret data.</summary>
    public bool TryVerify(Guid fileId, long exp, string disposition, string providedSignatureBase64Url)
    {
        byte[] provided;
        try
        {
            provided = Base64UrlDecode(providedSignatureBase64Url);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = ComputeSignature(fileId, exp, disposition);

        return provided.Length == expected.Length && CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    private byte[] ComputeSignature(Guid fileId, long exp, string disposition)
    {
        var key = Encoding.UTF8.GetBytes(options.Value.UrlSigningKey!);
        var payload = Encoding.UTF8.GetBytes($"{fileId:D}|{exp}|{disposition}");
        return HMACSHA256.HashData(key, payload);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
