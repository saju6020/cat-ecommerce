using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CatEcommerce.Read.QueryHandlers.StoredFile;

/// <summary>See <see cref="IStoredFileSigningBatcher"/>. One query against
/// <c>files.stored_file</c> for every requested id, then one signature per file -- no N+1 dispatch.
/// ADR-370: a file whose purpose is served straight from the bucket gets a <see cref="IDirectFileUrlSigner"/> URL
/// (signed in parallel, cached per window by the signer); every other file keeps the app-served
/// <see cref="IFileUrlSigner"/> URL. Access is unchanged: it is still the calling endpoint that decides who may see
/// which file before asking for its URL.
/// Gate fixes (CODE_REVIEW HIGH/MEDIUM): a copy is only pointed at once the store has it (checked once and remembered:
/// a photo uploaded before ADR-370 is served from its original until the backfill has made its copies, and one the
/// backfill cannot decode stays on its original); direct signing (an IAM call on the VM) runs at most
/// <see cref="MaxParallelSignatures"/> at a time; and a file whose direct signature fails falls back to its app-served
/// URL instead of failing the whole response.</summary>
public sealed class StoredFileSigningBatcher(
    CatEcommerceDbContext db,
    IFileUrlSigner signer,
    IDirectFileUrlSigner directSigner,
    IFileStore fileStore,
    IMemoryCache cache,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider,
    ILogger<StoredFileSigningBatcher> logger)
    : IStoredFileSigningBatcher
{
    internal const int MaxParallelSignatures = 8;
    private static readonly TimeSpan CopyPresentFor = TimeSpan.FromHours(12);
    private static readonly TimeSpan CopyAbsentFor = TimeSpan.FromMinutes(10);

    public Task<IReadOnlyDictionary<Guid, StoredFileViewModel>> SignManyAsync(
        IReadOnlyCollection<Guid> fileIds, string requestedDisposition, CancellationToken ct = default) =>
        SignManyAsync(fileIds, requestedDisposition, variant: null, ct);

    public async Task<IReadOnlyDictionary<Guid, StoredFileViewModel>> SignManyAsync(
        IReadOnlyCollection<Guid> fileIds, string requestedDisposition, FileVariant? variant, CancellationToken ct = default)
    {
        if (fileIds.Count == 0) return new Dictionary<Guid, StoredFileViewModel>();

        var rows = await db.StoredFiles.AsNoTracking()
            .Where(f => fileIds.Contains(f.Id))
            .Select(f => new { f.Id, f.Purpose, f.StorageKey, f.ContentType, f.SizeInBytes, f.OriginalFileName })
            .ToListAsync(ct);

        var signedUrlMinutes = businessSettings.GetInt(SettingKeys.FilesSignedUrlMinutes);
        var expiresIn = TimeSpan.FromMinutes(Math.Max(1, signedUrlMinutes));
        // Gate-fix 2026-09-27 (ARCHITECTURE LOW, third pass): TimeProvider, not DateTimeOffset.UtcNow.
        var urlExpiresOn = timeProvider.GetUtcNow().Add(expiresIn);

        using var gate = new SemaphoreSlim(MaxParallelSignatures);
        var signed = await Task.WhenAll(rows.Select(async row =>
        {
            StoredFileViewModel AppServed()
            {
                var disposition = FileServingRules.ResolveDisposition(row.ContentType, requestedDisposition);
                var url = signer.Sign(row.Id, expiresIn, disposition);
                return new StoredFileViewModel(row.Id, url, row.ContentType, row.SizeInBytes, row.OriginalFileName, urlExpiresOn);
            }

            if (!directSigner.IsEnabledFor(row.Purpose)) return AppServed();

            await gate.WaitAsync(ct);
            try
            {
                var copy = variant is not null && FileVariants.For(row.Purpose).Contains(variant)
                    && await CopyExistsAsync(variant.KeyFor(row.StorageKey), ct) ? variant : null;
                var key = copy?.KeyFor(row.StorageKey) ?? row.StorageKey;
                var contentType = copy is null ? row.ContentType : FileVariants.ContentType;
                var directDisposition = FileServingRules.ResolveDisposition(contentType, requestedDisposition);
                var direct = await directSigner.SignAsync(key, contentType, directDisposition, ct);
                return new StoredFileViewModel(row.Id, direct.Url, contentType, row.SizeInBytes, row.OriginalFileName, direct.ExpiresOn);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Direct URL for stored file {FileId} failed; serving it through the API instead", row.Id);
                return AppServed();
            }
            finally
            {
                gate.Release();
            }
        }));

        return signed.ToDictionary(f => f.FileId);
    }

    private async Task<bool> CopyExistsAsync(string copyKey, CancellationToken ct)
    {
        var cacheKey = "stored-file-copy-exists|" + copyKey;
        if (cache.TryGetValue(cacheKey, out bool known)) return known;
        var exists = await fileStore.ExistsAsync(copyKey, ct);
        cache.Set(cacheKey, exists, exists ? CopyPresentFor : CopyAbsentFor);
        return exists;
    }
}
