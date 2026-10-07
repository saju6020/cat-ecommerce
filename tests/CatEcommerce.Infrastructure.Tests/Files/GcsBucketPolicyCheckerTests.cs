using CatEcommerce.Infrastructure.Files;

namespace CatEcommerce.Infrastructure.Tests.Files;

/// <summary>ADR-318's own startup assertions -- gate-fix 2026-09-27 (ARCHITECTURE HIGH). No live
/// GCS bucket is available in this environment, so these are exercised against
/// <see cref="FakeStorageClient"/> (HO-FIL-001.md §1 Amended (7)).</summary>
public sealed class GcsBucketPolicyCheckerTests
{
    [Fact]
    public async Task AssertSecureAsync_passes_for_a_correctly_configured_bucket()
    {
        var sut = new GcsBucketPolicyChecker(FakeStorageClient.Secure());

        await sut.AssertSecureAsync("some-bucket", CancellationToken.None);
        // No exception -- the bucket satisfies every ADR-318 control.
    }

    [Fact]
    public async Task AssertSecureAsync_throws_when_uniform_bucket_level_access_is_disabled()
    {
        var sut = new GcsBucketPolicyChecker(FakeStorageClient.WithoutUniformAccess());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AssertSecureAsync("some-bucket", CancellationToken.None));
    }

    [Fact]
    public async Task AssertSecureAsync_throws_when_public_access_prevention_is_not_enforced()
    {
        var sut = new GcsBucketPolicyChecker(FakeStorageClient.WithoutPublicAccessPrevention());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AssertSecureAsync("some-bucket", CancellationToken.None));
    }

    [Fact]
    public async Task AssertSecureAsync_throws_when_versioning_is_enabled()
    {
        var sut = new GcsBucketPolicyChecker(FakeStorageClient.WithVersioningEnabled());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AssertSecureAsync("some-bucket", CancellationToken.None));
    }

    [Fact]
    public async Task AssertSecureAsync_throws_when_soft_delete_is_enabled()
    {
        var sut = new GcsBucketPolicyChecker(FakeStorageClient.WithSoftDeleteEnabled());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AssertSecureAsync("some-bucket", CancellationToken.None));
    }
}
