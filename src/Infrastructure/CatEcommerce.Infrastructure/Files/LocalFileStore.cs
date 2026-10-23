using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Files;

/// <summary>
/// HO-FIL-001 §8.4: the Development/Testing <see cref="IFileStore"/>, writing outside the content
/// root (never inside <c>wwwroot</c> or the Api project directory, so a stray relative path never
/// serves an uploaded file as a static asset). Never registered outside Development/Testing --
/// see <c>FileStorageModule</c>'s environment guard.
/// </summary>
public sealed class LocalFileStore(IOptions<FileStorageOptions> options, ILogger<LocalFileStore> logger) : IFileStore
{
    public async Task<FileStoreResult<string>> PutAsync(FilePutRequest request, CancellationToken ct)
    {
        var storageKey = StorageKeyFactory.Build(request.Purpose, request.FileId, DateTimeOffset.UtcNow);
        var path = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (request.Content.CanSeek) request.Content.Position = 0;

        await using var target = new FileStream(
            path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await request.Content.CopyToAsync(target, ct);

        return FileStoreResult<string>.Success(storageKey);
    }

    public Task<FileStoreResult<Stream>> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
        {
            // A row with no object is corruption, not a 404 to shrug at (§8.4) -- same treatment
            // as GcsFileStore, so I9 behaves identically regardless of which store is active.
            logger.LogError("Stored file object missing at storage key {StorageKey}", storageKey);
            return Task.FromResult(FileStoreResult<Stream>.Failure(
                BusinessViolationCodes.FileObjectMissing, $"No object at storage key '{storageKey}'."));
        }

        Stream stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        return Task.FromResult(FileStoreResult<Stream>.Success(stream));
    }

    public Task<FileStoreResult<Unit>> DeleteAsync(string storageKey, CancellationToken ct)
    {
        // A missing object is success -- idempotent, §8.4. ADR-370: the resized copies go with it.
        foreach (var key in FileVariants.ForStorageKey(storageKey).Select(v => v.KeyFor(storageKey)).Prepend(storageKey))
        {
            var path = ResolvePath(key);
            if (File.Exists(path)) File.Delete(path);
        }
        return Task.FromResult(FileStoreResult<Unit>.Success(Unit.Value));
    }

    public async Task<FileStoreResult<Unit>> PutVariantAsync(string storageKey, FileVariant variant, Stream content, CancellationToken ct)
    {
        var path = ResolvePath(variant.KeyFor(storageKey));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (content.CanSeek) content.Position = 0;

        await using var target = new FileStream(
            path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await content.CopyToAsync(target, ct);
        return FileStoreResult<Unit>.Success(Unit.Value);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct) => Task.FromResult(File.Exists(ResolvePath(storageKey)));

    /// <summary>Confines every path to <see cref="FileStorageOptions.LocalStoreRoot"/> -- a
    /// storage key is always server-generated (<see cref="StorageKeyFactory"/>), but this is a
    /// second, independent guard against path traversal for any key this store is ever asked to
    /// read or delete.</summary>
    private string ResolvePath(string storageKey)
    {
        var root = Path.GetFullPath(options.Value.LocalStoreRoot);
        var combined = Path.GetFullPath(Path.Combine(root, storageKey));

        if (!combined.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && combined != root)
            throw new InvalidOperationException($"Storage key '{storageKey}' resolves outside the local store root.");

        return combined;
    }
}
