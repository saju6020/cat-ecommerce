namespace CatEcommerce.Infrastructure.Files;

/// <summary>
/// Gate-fix 2026-09-27 (ARCHITECTURE HIGH): ADR-318's own startup assertions -- "uniform bucket-level
/// access, public-access prevention, versioning off, soft-delete off" -- were never built, only the
/// bucket-name and signing-key checks were. An interface so the assertion logic is unit-testable
/// against a fake <c>StorageClient</c> subclass without a live GCS bucket (none is available in
/// this environment, per HO-FIL-001.md §1 Amended (7)).
/// </summary>
public interface IGcsBucketPolicyChecker
{
    /// <summary>Throws <see cref="InvalidOperationException"/> naming the first control that is
    /// not in the state ADR-318 requires.</summary>
    Task AssertSecureAsync(string bucketName, CancellationToken ct);
}
