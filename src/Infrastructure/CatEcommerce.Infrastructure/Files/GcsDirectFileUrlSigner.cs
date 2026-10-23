using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Shared.Common.Enums;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Files;

/// <summary>
/// ADR-370: Cloud Storage V4 signed GET URLs for the purposes in <see cref="FileStorageOptions.DirectServePurposes"/>.
/// <list type="bullet">
/// <item>Time is cut into windows of <see cref="FileStorageOptions.DirectUrlWindowMinutes"/>. Every request in one window
/// gets the same URL (cached here until the window ends), valid until the end of the NEXT window, so a URL handed out at
/// the last second of a window still works for a whole window and the browser can reuse its cached copy meanwhile.</item>
/// <item>Signing uses the host's own credentials (ADC). On the VM that is the VM's service account, signed through the
/// IAM signBlob API (no key file, ADR-318); caching keeps that to one call per object per window. The signing time
/// (X-Goog-Date) is the signer's own system clock (the library offers no public clock hook); the windows follow
/// <see cref="TimeProvider"/>.</item>
/// <item>The URL forces the response's Content-Type and Content-Disposition, so the bucket cannot be made to serve
/// stored bytes as anything other than the type the upload pipeline verified.</item>
/// </list>
/// </summary>
public sealed class GcsDirectFileUrlSigner(
    UrlSigner urlSigner,
    IOptions<FileStorageOptions> options,
    IMemoryCache cache,
    TimeProvider timeProvider) : IDirectFileUrlSigner
{
    private readonly HashSet<string> _purposes = new(
        ParsePurposes(options.Value.DirectServePurposes).Where(MayBeServedDirectly), StringComparer.OrdinalIgnoreCase);

    /// <summary>The purposes a configuration value lists (comma separated; <c>none</c> or empty lists none).</summary>
    public static IEnumerable<string> ParsePurposes(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !p.Equals("none", StringComparison.OrdinalIgnoreCase));

    /// <summary>ADR-370: only public catalogue photos (no personal data) may skip the app-served path.</summary>
    public static bool MayBeServedDirectly(string purpose) =>
        purpose.Equals(FilePurpose.ProductImage.DisplayName, StringComparison.OrdinalIgnoreCase);

    public bool IsEnabledFor(FilePurpose purpose) => _purposes.Contains(purpose.DisplayName);

    public async Task<DirectFileUrl> SignAsync(string storageKey, string contentType, string disposition, CancellationToken ct)
    {
        var bucket = options.Value.GcsBucketName!;
        var window = Window(options.Value);
        var now = timeProvider.GetUtcNow();
        var windowStart = new DateTimeOffset(now.UtcTicks - (now.UtcTicks % window.Ticks), TimeSpan.Zero);
        var cacheKey = $"direct-file-url|{bucket}|{storageKey}|{contentType}|{disposition}|{windowStart.UtcTicks}";

        if (cache.TryGetValue(cacheKey, out DirectFileUrl? cached) && cached is not null) return cached;

        var expiresOn = windowStart + window + window;
        var template = UrlSigner.RequestTemplate
            .FromBucket(bucket)
            .WithObjectName(storageKey)
            .WithHttpMethod(HttpMethod.Get)
            .WithQueryParameters(new Dictionary<string, IEnumerable<string>>
            {
                ["response-content-type"] = [contentType],
                ["response-content-disposition"] = [disposition],
            });
        var signOptions = UrlSigner.Options.FromExpiration(expiresOn).WithSigningVersion(SigningVersion.V4);
        var url = await urlSigner.SignAsync(template, signOptions, ct);

        var signed = new DirectFileUrl(url, expiresOn);
        cache.Set(cacheKey, signed, windowStart + window);
        return signed;
    }

    internal static TimeSpan Window(FileStorageOptions options) =>
        TimeSpan.FromMinutes(Math.Clamp(options.DirectUrlWindowMinutes, 5, 30));
}

/// <summary>ADR-370: where the store cannot sign (Development/Testing's local store), every file stays app-served.</summary>
public sealed class NoDirectFileUrlSigner : IDirectFileUrlSigner
{
    public bool IsEnabledFor(FilePurpose purpose) => false;

    public Task<DirectFileUrl> SignAsync(string storageKey, string contentType, string disposition, CancellationToken ct) =>
        throw new InvalidOperationException("Direct file URLs are not available with this file store.");
}
