using System.Security.Cryptography;
using System.Web;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Files;
using CatEcommerce.Shared.Common.Enums;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Infrastructure.Tests.Files;

/// <summary>ADR-370: direct-from-bucket V4 signed URLs, signed offline with a throw-away service-account key.</summary>
public sealed class GcsDirectFileUrlSignerTests
{
    private const string Key = "ProductImage/2026/10/0123456789abcdef0123456789abcdef.w480.webp";
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);

    private GcsDirectFileUrlSigner CreateSut(IMemoryCache? cache = null)
    {
        using var rsa = RSA.Create(2048);
        var credential = new ServiceAccountCredential(
            new ServiceAccountCredential.Initializer("signer@test-project.iam.gserviceaccount.com").FromPrivateKey(rsa.ExportPkcs8PrivateKeyPem()));
        return new GcsDirectFileUrlSigner(
            UrlSigner.FromCredential(credential),
            Options.Create(new FileStorageOptions { GcsBucketName = "purrcare-files-test", DirectUrlWindowMinutes = 30 }),
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            _time);
    }

    [Fact]
    public void Only_the_configured_purposes_are_served_straight_from_the_bucket()
    {
        var sut = CreateSut();
        Assert.True(sut.IsEnabledFor(FilePurpose.ProductImage));
        Assert.False(sut.IsEnabledFor(FilePurpose.ClinicalPhoto));
        Assert.False(sut.IsEnabledFor(FilePurpose.CatProfilePhoto));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("")]
    public void Direct_serving_can_be_switched_off_in_configuration(string purposes)
    {
        using var rsa = RSA.Create(2048);
        var credential = new ServiceAccountCredential(
            new ServiceAccountCredential.Initializer("signer@test-project.iam.gserviceaccount.com").FromPrivateKey(rsa.ExportPkcs8PrivateKeyPem()));
        var sut = new GcsDirectFileUrlSigner(UrlSigner.FromCredential(credential),
            Options.Create(new FileStorageOptions { GcsBucketName = "b", DirectServePurposes = purposes }), new MemoryCache(new MemoryCacheOptions()), _time);

        Assert.False(sut.IsEnabledFor(FilePurpose.ProductImage));
    }

    [Fact]
    public async Task The_url_is_a_v4_signed_bucket_url_that_forces_the_verified_type_and_disposition()
    {
        var signed = await CreateSut().SignAsync(Key, "image/webp", "inline", CancellationToken.None);

        var uri = new Uri(signed.Url);
        Assert.Equal("storage.googleapis.com", uri.Host);
        Assert.Equal("/purrcare-files-test/" + Key, Uri.UnescapeDataString(uri.AbsolutePath));
        var query = HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal("GOOG4-RSA-SHA256", query["X-Goog-Algorithm"]);
        Assert.Equal("image/webp", query["response-content-type"]);
        Assert.Equal("inline", query["response-content-disposition"]);
        Assert.False(string.IsNullOrEmpty(query["X-Goog-Signature"]));
    }

    [Fact]
    public async Task One_window_hands_out_one_url_so_the_browser_can_cache_the_image()
    {
        var sut = CreateSut();
        var first = await sut.SignAsync(Key, "image/webp", "inline", CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(30));
        var second = await sut.SignAsync(Key, "image/webp", "inline", CancellationToken.None);

        Assert.Equal(first.Url, second.Url);
    }

    [Fact]
    public async Task The_next_window_gets_a_new_url_and_every_url_stays_valid_for_at_least_one_window()
    {
        var sut = CreateSut();
        var first = await sut.SignAsync(Key, "image/webp", "inline", CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(31));
        var later = await sut.SignAsync(Key, "image/webp", "inline", CancellationToken.None);

        Assert.NotEqual(first.Url, later.Url);
        Assert.True(later.ExpiresOn - _time.GetUtcNow() >= TimeSpan.FromMinutes(30));
        Assert.True(later.ExpiresOn - _time.GetUtcNow() <= TimeSpan.FromMinutes(60));
    }

    [Fact]
    public async Task Different_objects_never_share_a_url()
    {
        var sut = CreateSut();
        var card = await sut.SignAsync(Key, "image/webp", "inline", CancellationToken.None);
        var detail = await sut.SignAsync(Key.Replace(".w480.", ".w1200."), "image/webp", "inline", CancellationToken.None);

        Assert.NotEqual(card.Url, detail.Url);
    }
}
