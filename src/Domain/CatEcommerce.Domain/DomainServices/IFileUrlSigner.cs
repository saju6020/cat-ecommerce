namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Owned by HO-FIL-001 §8.6. "A consuming read asks <c>IFileUrlSigner</c> for a URL and returns it
/// with its view model":
/// <c>/api/files/{fileId}/content?exp={unix}&amp;disp={inline|attachment}&amp;sig={base64url}</c>
/// where <c>sig = HMAC-SHA256(FileStorage:UrlSigningKey, "{fileId}|{exp}|{disp}")</c> and
/// <c>exp = now + 5 min</c>.
///
/// Real adapter: <c>HmacFileUrlSigner</c> (Infrastructure/Files). Gate-fix 2026-09-27 (CODE_REVIEW
/// LOW): <see cref="TryVerify"/> was previously only on the concrete class, so
/// <c>FilesController</c> had to depend on it directly instead of this port -- moved onto the
/// interface so the controller can depend on the abstraction like every other domain service.
/// </summary>
public interface IFileUrlSigner
{
    string Sign(Guid fileId, TimeSpan expiresIn, string disposition);

    /// <summary>Constant-time signature check (U8). Refuses a tampered signature; does not itself
    /// check <paramref name="exp"/> against the current time -- the caller does that separately,
    /// since expiry is not secret data.</summary>
    bool TryVerify(Guid fileId, long exp, string disposition, string providedSignatureBase64Url);
}
