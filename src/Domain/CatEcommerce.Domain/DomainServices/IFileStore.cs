using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Owned by HO-FIL-001 (ADR-318). One port, one place a file is accepted, scanned, stored and
/// served for the whole solution -- resolves README C-5. Exact signature from ADR-318's own code
/// block; not paraphrased.
///
/// Real adapters: <c>GcsFileStore</c> (Staging/Production) and <c>LocalFileStore</c>
/// (Development/Testing), both in Infrastructure/Files. Gate-fix 2026-09-27: the placeholder
/// in-memory fake this doc comment used to point to (<c>Fakes/FileStorage/InMemoryFileStore</c>)
/// is deleted now that both real adapters exist and are registered.
/// </summary>
public interface IFileStore
{
    Task<FileStoreResult<string>> PutAsync(FilePutRequest request, CancellationToken ct);

    Task<FileStoreResult<Stream>> OpenReadAsync(string storageKey, CancellationToken ct);

    /// <summary>Removes the object and the copies stored beside it (<see cref="FileVariants.ForStorageKey"/>). Idempotent.</summary>
    Task<FileStoreResult<Unit>> DeleteAsync(string storageKey, CancellationToken ct);

    /// <summary>ADR-370: stores a resized copy beside an existing object, at <see cref="FileVariant.KeyFor"/>.</summary>
    Task<FileStoreResult<Unit>> PutVariantAsync(string storageKey, FileVariant variant, Stream content, CancellationToken ct);

    /// <summary>ADR-370: whether an object (an original or a copy's key) exists; the backfill uses it to resume.</summary>
    Task<bool> ExistsAsync(string storageKey, CancellationToken ct);
}
