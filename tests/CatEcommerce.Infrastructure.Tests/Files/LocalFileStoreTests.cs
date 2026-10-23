using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Files;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Tests.Files;

public sealed class LocalFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "catecommerce-fil-tests-" + Guid.NewGuid().ToString("N"));

    private LocalFileStore CreateSut() =>
        new(Options.Create(new FileStorageOptions { LocalStoreRoot = _root }), NullLogger<LocalFileStore>.Instance);

    [Fact]
    public async Task PutAsync_then_OpenReadAsync_round_trips_the_bytes()
    {
        var sut = CreateSut();
        var bytes = "hello"u8.ToArray();
        var request = new FilePutRequest(Guid.NewGuid(), FilePurpose.ProductImage, new MemoryStream(bytes), "image/png", bytes.Length, "sha");

        var put = await sut.PutAsync(request, CancellationToken.None);
        Assert.True(put.IsSuccess);

        var read = await sut.OpenReadAsync(put.Value!, CancellationToken.None);
        Assert.True(read.IsSuccess);

        using var buffer = new MemoryStream();
        await read.Value!.CopyToAsync(buffer);
        await read.Value!.DisposeAsync();
        Assert.Equal(bytes, buffer.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_for_an_unknown_key_reports_FileObjectMissing()
    {
        var sut = CreateSut();

        var result = await sut.OpenReadAsync("ProductImage/2026/09/doesnotexist", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("FileObjectMissing", result.ViolationCode);
    }

    [Fact]
    public async Task DeleteAsync_of_a_missing_object_is_idempotent_success()
    {
        var sut = CreateSut();

        var result = await sut.DeleteAsync("ProductImage/2026/09/doesnotexist", CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteAsync_then_OpenReadAsync_reports_the_object_gone()
    {
        var sut = CreateSut();
        var request = new FilePutRequest(Guid.NewGuid(), FilePurpose.ProductImage, new MemoryStream("x"u8.ToArray()), "image/png", 1, "sha");
        var put = await sut.PutAsync(request, CancellationToken.None);

        var delete = await sut.DeleteAsync(put.Value!, CancellationToken.None);
        Assert.True(delete.IsSuccess);

        var read = await sut.OpenReadAsync(put.Value!, CancellationToken.None);
        Assert.False(read.IsSuccess);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\config")]
    public async Task OpenReadAsync_refuses_a_key_that_would_escape_the_store_root(string maliciousKey)
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.OpenReadAsync(maliciousKey, CancellationToken.None));
    }

    // ADR-370: the resized copies live beside the original and are deleted with it.
    [Fact]
    public async Task A_product_photo_copy_is_stored_beside_it_and_deleted_with_it()
    {
        var sut = CreateSut();
        var put = await sut.PutAsync(new FilePutRequest(Guid.NewGuid(), FilePurpose.ProductImage, new MemoryStream("x"u8.ToArray()), "image/png", 1, "sha"), CancellationToken.None);
        var card = CatEcommerce.Shared.Common.Constants.FileVariants.Card;

        Assert.True((await sut.PutVariantAsync(put.Value!, card, new MemoryStream("webp"u8.ToArray()), CancellationToken.None)).IsSuccess);
        Assert.True(await sut.ExistsAsync(card.KeyFor(put.Value!), CancellationToken.None));

        await sut.DeleteAsync(put.Value!, CancellationToken.None);

        Assert.False(await sut.ExistsAsync(put.Value!, CancellationToken.None));
        Assert.False(await sut.ExistsAsync(card.KeyFor(put.Value!), CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
