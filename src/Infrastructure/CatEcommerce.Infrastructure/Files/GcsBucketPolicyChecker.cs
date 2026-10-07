using Google.Cloud.Storage.V1;

namespace CatEcommerce.Infrastructure.Files;

/// <summary>ADR-318: "all of it asserted at startup" -- uniform bucket-level access, public-access
/// prevention enforced, versioning off, soft-delete off. Called once, from
/// <c>FileStorageStartupGuardHostedService</c>, outside Development/Testing.</summary>
public sealed class GcsBucketPolicyChecker(StorageClient client) : IGcsBucketPolicyChecker
{
    public async Task AssertSecureAsync(string bucketName, CancellationToken ct)
    {
        var bucket = await client.GetBucketAsync(bucketName, cancellationToken: ct);

        if (bucket.IamConfiguration?.UniformBucketLevelAccess?.Enabled != true)
        {
            throw new InvalidOperationException(
                $"GCS bucket '{bucketName}' does not have uniform bucket-level access enabled (ADR-318).");
        }

        if (!string.Equals(bucket.IamConfiguration?.PublicAccessPrevention, "enforced", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"GCS bucket '{bucketName}' does not enforce public access prevention (ADR-318).");
        }

        if (bucket.Versioning?.Enabled == true)
        {
            throw new InvalidOperationException(
                $"GCS bucket '{bucketName}' has object versioning enabled; ADR-318 requires it off.");
        }

        if ((bucket.SoftDeletePolicy?.RetentionDurationSeconds ?? 0) > 0)
        {
            throw new InvalidOperationException(
                $"GCS bucket '{bucketName}' has soft-delete enabled (retention duration > 0); ADR-318/V5 requires it off.");
        }
    }
}
