using Google.Apis.Storage.v1.Data;
using Google.Cloud.Storage.V1;

namespace CatEcommerce.Infrastructure.Tests.Files;

/// <summary>Hand-written <see cref="StorageClient"/> test double -- <c>GetBucketAsync</c> is
/// virtual specifically so the SDK's own client can be faked for exactly this purpose. No live GCS
/// bucket is available in this environment (HO-FIL-001.md §1 Amended (7)).</summary>
internal sealed class FakeStorageClient : StorageClient
{
    private readonly Bucket _bucket;
    private readonly int? _downloadFailAfterBytes;
    private readonly byte[]? _downloadContent;

    private FakeStorageClient(Bucket bucket, int? downloadFailAfterBytes = null, byte[]? downloadContent = null)
    {
        _bucket = bucket;
        _downloadFailAfterBytes = downloadFailAfterBytes;
        _downloadContent = downloadContent;
    }

    /// <summary>Gate-fix 2026-09-27 (SECURITY LOW): <c>GcsFileStore.OpenReadAsync</c>'s
    /// <c>GetObjectAsync</c> existence check succeeds, then <c>DownloadObjectAsync</c> writes
    /// <paramref name="failAfterBytes"/> real bytes to the destination stream before throwing --
    /// simulating a network reset or the SDK's own CRC32C/MD5 validation failure mid-download, to
    /// prove the reader sees the exception rather than a clean, truncated end of stream.</summary>
    public static FakeStorageClient ForObjectDownloadFailure(int failAfterBytes) => new(new Bucket(), failAfterBytes);

    /// <summary>TEST gate (2026-09-27): the happy path -- <c>DownloadObjectAsync</c> writes the
    /// whole of <paramref name="content"/> to the destination stream and returns normally, so
    /// <c>GcsFileStore.OpenReadAsync</c>'s <see cref="System.IO.Pipelines.Pipe"/> bridge can be
    /// proven to deliver the object's bytes back to the reader byte-for-byte, not just to surface a
    /// failure correctly (the only case this double supported before this gate).</summary>
    public static FakeStorageClient ForObjectDownloadSuccess(byte[] content) => new(new Bucket(), downloadContent: content);

    public static FakeStorageClient Secure() => new(new Bucket
    {
        IamConfiguration = new Bucket.IamConfigurationData
        {
            UniformBucketLevelAccess = new Bucket.IamConfigurationData.UniformBucketLevelAccessData { Enabled = true },
            PublicAccessPrevention = "enforced"
        },
        Versioning = new Bucket.VersioningData { Enabled = false },
        SoftDeletePolicy = new Bucket.SoftDeletePolicyData { RetentionDurationSeconds = 0 }
    });

    public static FakeStorageClient WithoutUniformAccess() => Mutate(Secure(), b => b.IamConfiguration!.UniformBucketLevelAccess!.Enabled = false);

    public static FakeStorageClient WithoutPublicAccessPrevention() => Mutate(Secure(), b => b.IamConfiguration!.PublicAccessPrevention = "inherited");

    public static FakeStorageClient WithVersioningEnabled() => Mutate(Secure(), b => b.Versioning!.Enabled = true);

    public static FakeStorageClient WithSoftDeleteEnabled() => Mutate(Secure(), b => b.SoftDeletePolicy!.RetentionDurationSeconds = 604800);

    private static FakeStorageClient Mutate(FakeStorageClient template, Action<Bucket> mutate)
    {
        mutate(template._bucket);
        return template;
    }

    public override Task<Bucket> GetBucketAsync(string bucket, GetBucketOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(_bucket);

    public override Task<Google.Apis.Storage.v1.Data.Object> GetObjectAsync(
        string bucket, string objectName, GetObjectOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Google.Apis.Storage.v1.Data.Object { Bucket = bucket, Name = objectName });

    public override async Task<Google.Apis.Storage.v1.Data.Object> DownloadObjectAsync(
        string bucket, string objectName, System.IO.Stream destination, DownloadObjectOptions? options = null,
        CancellationToken cancellationToken = default, IProgress<Google.Apis.Download.IDownloadProgress>? progress = null)
    {
        if (_downloadContent is { } content)
        {
            // Written in more than one chunk, matching how a real network download arrives, so
            // this exercises the Pipe bridge's chunked-write path rather than a single WriteAsync.
            const int chunkSize = 64 * 1024;
            for (var offset = 0; offset < content.Length; offset += chunkSize)
            {
                var length = Math.Min(chunkSize, content.Length - offset);
                await destination.WriteAsync(content.AsMemory(offset, length), cancellationToken);
            }

            await destination.FlushAsync(cancellationToken);
            return new Google.Apis.Storage.v1.Data.Object { Bucket = bucket, Name = objectName, Size = (ulong)content.Length };
        }

        if (_downloadFailAfterBytes is not { } failAfterBytes)
            throw new InvalidOperationException("This FakeStorageClient was not configured for a download.");

        var chunk = new byte[failAfterBytes];
        await destination.WriteAsync(chunk, cancellationToken);
        await destination.FlushAsync(cancellationToken);
        throw new IOException("Simulated network reset mid-download (test double).");
    }
}
