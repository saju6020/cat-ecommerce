using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Api.Background;

/// <summary>
/// ADR-370: makes the resized copies that older product photos do not have yet (uploads before ADR-370). Runs once at
/// startup, only when <see cref="FileStorageOptions.BackfillVariants"/> is on, and skips every copy that already
/// exists, so it can be stopped and run again. A photo it cannot read or decode is logged and left as it is.
/// </summary>
public sealed class ImageVariantBackfillWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<FileStorageOptions> options,
    ILogger<ImageVariantBackfillWorker> logger) : BackgroundService
{
    private const int Parallelism = 4;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.BackfillVariants) return;

        List<string> keys;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            keys = await db.StoredFiles.AsNoTracking()
                .Where(f => f.Purpose == FilePurpose.ProductImage)
                .Select(f => f.StorageKey)
                .ToListAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Gate fix (CODE_REVIEW MEDIUM): an exception escaping ExecuteAsync would stop the whole host.
            logger.LogError(ex, "Image copy backfill could not list the product photos; it will run at the next start");
            return;
        }

        logger.LogInformation("Image copy backfill: checking {Count} product photos", keys.Count);
        int made = 0, complete = 0, failed = 0;

        await Parallel.ForEachAsync(keys, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = stoppingToken },
            async (key, ct) =>
            {
                using var scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IFileStore>();
                var generator = scope.ServiceProvider.GetRequiredService<IImageVariantGenerator>();
                try
                {
                    var missing = new List<FileVariant>();
                    foreach (var variant in FileVariants.For(FilePurpose.ProductImage))
                        if (!await store.ExistsAsync(variant.KeyFor(key), ct)) missing.Add(variant);
                    if (missing.Count == 0) { Interlocked.Increment(ref complete); return; }

                    var original = await store.OpenReadAsync(key, ct);
                    if (!original.IsSuccess) { Interlocked.Increment(ref failed); return; }
                    using var buffer = new MemoryStream();
                    await using (var stream = original.Value!) await stream.CopyToAsync(buffer, ct);

                    var copies = generator.Generate(buffer, missing);
                    if (copies is null)
                    {
                        logger.LogWarning("Image copy backfill: {StorageKey} could not be decoded", key);
                        Interlocked.Increment(ref failed);
                        return;
                    }
                    foreach (var copy in copies)
                    {
                        using var content = new MemoryStream(copy.Content, writable: false);
                        await store.PutVariantAsync(key, copy.Variant, content, ct);
                    }

                    // SECURITY gate (LOW): the photo may have been erased while its copies were being made; copies
                    // written after that would have no row and nothing would ever delete them.
                    if (!await store.ExistsAsync(key, ct))
                    {
                        await store.DeleteAsync(key, CancellationToken.None);
                        return;
                    }
                    Interlocked.Increment(ref made);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Image copy backfill: {StorageKey} failed", key);
                    Interlocked.Increment(ref failed);
                }
            });

        logger.LogInformation(
            "Image copy backfill finished: {Made} photos given copies, {Complete} already complete, {Failed} failed",
            made, complete, failed);
    }
}
