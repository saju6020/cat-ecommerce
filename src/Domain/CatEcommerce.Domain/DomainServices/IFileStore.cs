using CatEcommerce.Domain.Models;
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

    Task<FileStoreResult<Unit>> DeleteAsync(string storageKey, CancellationToken ct);
}
