using System.IO.Pipelines;
using System.Net;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;
using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Files;

/// <summary>
/// HO-FIL-001 §8.4 (ADR-318): <c>Google.Cloud.Storage.V1</c>, one <see cref="StorageClient"/>
/// (Singleton, thread-safe -- injected here, registered once by <c>FileStorageModule</c>). Never
/// registered outside Staging/Production -- see the module's environment guard.
/// </summary>
public sealed class GcsFileStore(
    StorageClient client,
    IOptions<FileStorageOptions> options,
    ILogger<GcsFileStore> logger) : IFileStore
{
    public async Task<FileStoreResult<string>> PutAsync(FilePutRequest request, CancellationToken ct)
    {
        var storageKey = StorageKeyFactory.Build(request.Purpose, request.FileId, DateTimeOffset.UtcNow);
        var bucket = options.Value.GcsBucketName!;

        if (request.Content.CanSeek) request.Content.Position = 0;

        var gcsObject = new Google.Apis.Storage.v1.Data.Object
        {
            Bucket = bucket,
            Name = storageKey,
            ContentType = request.ContentType,
            CacheControl = "private, no-store",
            Metadata = new Dictionary<string, string>
            {
                ["fileId"] = request.FileId.ToString("D"),
                ["purpose"] = request.Purpose.DisplayName,
                ["sha256"] = request.Sha256
            }
        };

        await client.UploadObjectAsync(gcsObject, request.Content, cancellationToken: ct);

        return FileStoreResult<string>.Success(storageKey);
    }

    /// <summary>
    /// Gate-fix 2026-09-27 (SECURITY MEDIUM, HO-FIL-001-SECURITY.md): previously downloaded the
    /// whole object into a <see cref="MemoryStream"/> before returning it -- V4 ("never buffered in
    /// memory") applies to serving as much as it does to uploading, and a 50 MB object fetched
    /// concurrently by enough callers could exhaust process memory (the signed URL is a bearer
    /// token replayable for its whole lifetime, with no serving-side rate limit before this gate
    /// pass). A cheap metadata call confirms the object exists (and lets a missing object still be
    /// reported as <c>FileObjectMissing</c> before any streaming begins); the actual bytes are then
    /// piped from the SDK's own destination-stream download API to the caller via
    /// <see cref="Pipe"/>, so memory use stays bounded by the pipe's own buffer regardless of
    /// object size -- unverified against a live bucket (none available here, HO-FIL-001.md §1
    /// Amended (7)), but the SDK exposes no direct "open a read stream" method, only "download into
    /// a stream I already have", which is exactly what a <see cref="Pipe"/> bridges.
    /// </summary>
    public async Task<FileStoreResult<Stream>> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var bucket = options.Value.GcsBucketName!;

        try
        {
            await client.GetObjectAsync(bucket, storageKey, cancellationToken: ct);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // A row with no object is corruption, not a 404 to shrug at (§8.4).
            logger.LogError(ex, "Stored file object missing from bucket for storage key {StorageKey}", storageKey);
            return FileStoreResult<Stream>.Failure(
                BusinessViolationCodes.FileObjectMissing, $"No object at storage key '{storageKey}'.");
        }

        var pipe = new Pipe();

        // Started, not awaited: the SDK writes into the pipe while the caller reads from it
        // concurrently. Deliberately not linked to a using/await-using disposal here -- the pipe
        // writer's own completion (success or failure) is what signals the reader side to stop.
        _ = Task.Run(async () =>
        {
            try
            {
                // Gate-fix 2026-09-27 (SECURITY LOW): leaveOpen: true is load-bearing. The default
                // (false) makes this stream's own Dispose/DisposeAsync call PipeWriter.Complete()
                // -- with no exception, unconditionally -- as soon as the `await using` block exits,
                // which happens on the exception path too (disposal runs during exception unwinding,
                // exactly like a `finally`). That meant a mid-download failure (a network reset, or
                // the SDK's own CRC32C/MD5 validation failing) was reported to the pipe's READER as
                // a clean, successful end-of-stream BEFORE the catch below ever got to call
                // CompleteAsync(ex) -- the reader saw a truncated-but-"successful" 200 response
                // instead of the download's own exception. leaveOpen: true means disposing
                // writerStream does nothing to the underlying PipeWriter, so CompleteAsync(...) in
                // this try/catch is the ONLY thing that ever completes it, on both paths.
                var writerStream = pipe.Writer.AsStream(leaveOpen: true);
                await using (writerStream)
                {
                    await client.DownloadObjectAsync(bucket, storageKey, writerStream, cancellationToken: ct);
                }

                await pipe.Writer.CompleteAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to stream stored file object for storage key {StorageKey}", storageKey);
                await pipe.Writer.CompleteAsync(ex);
            }
        }, ct);

        return FileStoreResult<Stream>.Success(pipe.Reader.AsStream());
    }

    public async Task<FileStoreResult<Unit>> DeleteAsync(string storageKey, CancellationToken ct)
    {
        var bucket = options.Value.GcsBucketName!;

        try
        {
            await client.DeleteObjectAsync(bucket, storageKey, cancellationToken: ct);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // Idempotent -- a missing object is success (§8.4); the retention worker may retry.
        }

        return FileStoreResult<Unit>.Success(Unit.Value);
    }
}
