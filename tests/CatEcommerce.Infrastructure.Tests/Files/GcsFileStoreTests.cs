using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Files;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Tests.Files;

/// <summary>
/// Gate-fix 2026-09-27 (SECURITY LOW, HO-FIL-001-SECURITY.md second pass): a mid-download failure
/// must surface as an exception on the reader, not a clean, truncated end of stream -- no live GCS
/// bucket is available in this environment, so this is exercised against
/// <see cref="FakeStorageClient"/> (HO-FIL-001.md §1 Amended (7)).
/// </summary>
public sealed class GcsFileStoreTests
{
    private static GcsFileStore CreateSut(Google.Cloud.Storage.V1.StorageClient client) =>
        new(client, Options.Create(new FileStorageOptions { GcsBucketName = "some-bucket" }), NullLogger<GcsFileStore>.Instance);

    [Fact]
    public async Task OpenReadAsync_a_mid_download_failure_surfaces_as_an_exception_on_the_reader_not_a_clean_truncated_end()
    {
        const int bytesBeforeFailure = 300_000;
        var sut = CreateSut(FakeStorageClient.ForObjectDownloadFailure(bytesBeforeFailure));

        var result = await sut.OpenReadAsync("some/storage/key", CancellationToken.None);
        Assert.True(result.IsSuccess);

        await using var stream = result.Value!;
        var buffer = new byte[bytesBeforeFailure + 4096];
        var totalRead = 0;

        var thrown = await Record.ExceptionAsync(async () =>
        {
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(totalRead))) > 0)
            {
                totalRead += read;
            }
        });

        // The reader must see the failure -- either as a thrown exception while still reading, or,
        // failing that, the stream must NOT silently report a clean end after only the bytes
        // written before the simulated failure (which is exactly the truncated-200 bug: the pipe
        // completing successfully despite the download never finishing).
        Assert.NotNull(thrown);
        Assert.IsType<IOException>(thrown);
    }

    // TEST gate (2026-09-27): the happy path was previously exercised only by the SECURITY gate's
    // own throwaway probe against a 60 MB fake object (never persisted) -- this proves the
    // Pipe-based bridge in OpenReadAsync delivers the object's bytes back to the reader
    // byte-for-byte, across a multi-chunk download, not just that a failure surfaces correctly.
    [Fact]
    public async Task OpenReadAsync_streams_the_full_object_content_back_byte_for_byte()
    {
        var content = new byte[5 * 1024 * 1024]; // 5 MB -- several times the fake client's 64 KB write chunk
        new Random(42).NextBytes(content);
        var sut = CreateSut(FakeStorageClient.ForObjectDownloadSuccess(content));

        var result = await sut.OpenReadAsync("some/storage/key", CancellationToken.None);
        Assert.True(result.IsSuccess);

        await using var stream = result.Value!;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        Assert.Equal(content.Length, buffer.Length);
        Assert.True(content.AsSpan().SequenceEqual(buffer.ToArray()));
    }
}
