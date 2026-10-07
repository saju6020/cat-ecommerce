namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>Bound from appsettings.json section "FileStorage" -- HO-FIL-001 §8.2/§8.4/§8.5/§8.6.
/// Bucket, credentials, scanner host and the signing key are configuration, never
/// administrator-editable settings (§10.3) -- they are not an administrator's to change at
/// runtime.</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>§8.2 step 1: where the upload pipeline streams and hashes an incoming file before
    /// any of it leaves the machine. Must be a git-ignored, local-only path.</summary>
    public string TempPath { get; set; } = Path.Combine(Path.GetTempPath(), "catecommerce-file-uploads");

    /// <summary>Development/Testing only (<see cref="Files.LocalFileStore"/>) -- written outside
    /// the content root (§8.4). Must be a git-ignored, local-only path.</summary>
    public string LocalStoreRoot { get; set; } = Path.Combine(Path.GetTempPath(), "catecommerce-file-store");

    /// <summary>Google Cloud Storage bucket name -- required outside Development/Testing (§8.4).</summary>
    public string? GcsBucketName { get; set; }

    /// <summary>ClamAV <c>clamd</c> host -- required outside Development/Testing (§8.5); unset
    /// there selects <see cref="Files.UnconfiguredMalwareScanner"/> (fail closed, ADR-319).</summary>
    public string? ClamAvHost { get; set; }

    public int ClamAvPort { get; set; } = 3310;

    /// <summary>HMAC-SHA256 key for <see cref="Files.HmacFileUrlSigner"/> (§8.6). >= 32 bytes,
    /// from configuration outside the repository; startup refuses a missing or short key outside
    /// Development/Testing.</summary>
    public string? UrlSigningKey { get; set; }

    // Gate-fix 2026-09-27 (ARCHITECTURE MEDIUM): ScanTimeoutSeconds and SignedUrlMinutes moved to
    // the ADM settings registry (SettingKeys.FilesScanTimeoutSeconds/FilesSignedUrlMinutes,
    // read via IBusinessSettings) -- §10.3 reserves plain configuration for the bucket,
    // credentials, scanner host and the signing key only.
}
