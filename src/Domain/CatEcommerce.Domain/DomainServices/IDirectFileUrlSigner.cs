using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// ADR-370 (owner, 2026-10-18): a short-lived URL that lets the browser fetch an object straight from the object store
/// (a Cloud Storage V4 signed URL) instead of through <c>GET /api/files/{id}/content</c>. Used only for the purposes the
/// configuration lists; every other file keeps the app-served <see cref="IFileUrlSigner"/> URL. Who may see a file is
/// still decided by the endpoint that asks for the URL, exactly as before.
/// </summary>
public interface IDirectFileUrlSigner
{
    /// <summary>False everywhere the store cannot sign (Development/Testing's local store), and for unlisted purposes.</summary>
    bool IsEnabledFor(FilePurpose purpose);

    /// <summary>
    /// The URL for one object. The same object gets the same URL for a whole time window, so browsers can cache it;
    /// <see cref="DirectFileUrl.ExpiresOn"/> is always at least one window away.
    /// </summary>
    Task<DirectFileUrl> SignAsync(string storageKey, string contentType, string disposition, CancellationToken ct);
}

public sealed record DirectFileUrl(string Url, DateTimeOffset ExpiresOn);
