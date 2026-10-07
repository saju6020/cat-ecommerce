using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Options;
using nClam;

namespace CatEcommerce.Infrastructure.Files;

/// <summary>
/// HO-FIL-001 §8.5 (ADR-319): <c>nClam</c> over TCP to <c>clamd</c>. A timeout is
/// <see cref="ScanOutcome.Unavailable"/>, never <see cref="ScanOutcome.Clean"/> (fail closed) --
/// the whole point of ADR-319. Registered only outside Development/Testing, and only when
/// <see cref="FileStorageOptions.ClamAvHost"/> is configured -- see <c>FileStorageModule</c>.
/// </summary>
public sealed class ClamAvScanner(IOptions<FileStorageOptions> options, IBusinessSettings businessSettings) : IMalwareScanner
{
    public async Task<ScanResult> ScanAsync(Stream content, CancellationToken ct)
    {
        var settings = options.Value;
        var client = new ClamClient(settings.ClamAvHost!, settings.ClamAvPort);

        // Gate-fix 2026-09-27 (ARCHITECTURE MEDIUM): §10.3 setting, not plain configuration.
        var scanTimeoutSeconds = businessSettings.GetInt(SettingKeys.FilesScanTimeoutSeconds);
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(scanTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        if (content.CanSeek) content.Position = 0;

        try
        {
            var result = await client.SendAndScanFileAsync(content, linkedCts.Token);

            return result.Result switch
            {
                ClamScanResults.Clean => new ScanResult(ScanOutcome.Clean, await ScannerNameAsync(client, ct)),
                ClamScanResults.VirusDetected => new ScanResult(
                    ScanOutcome.Infected,
                    $"ClamAV {result.InfectedFiles?.FirstOrDefault()?.VirusName ?? "unknown-signature"}"),
                // Unknown/Error from clamd itself -- fail closed, not Clean (ADR-319).
                _ => new ScanResult(ScanOutcome.Unavailable, null)
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Our own timeout fired, not the caller's cancellation -- Unavailable (ADR-319).
            return new ScanResult(ScanOutcome.Unavailable, null);
        }
        catch (Exception) when (ct is { IsCancellationRequested: false })
        {
            // Connection refused, DNS failure, protocol error against clamd -- fail closed.
            return new ScanResult(ScanOutcome.Unavailable, null);
        }
    }

    private static async Task<string> ScannerNameAsync(ClamClient client, CancellationToken ct)
    {
        try
        {
            var version = await client.GetVersionAsync(ct);
            return string.IsNullOrWhiteSpace(version) ? "ClamAV" : version.Trim();
        }
        catch
        {
            return "ClamAV";
        }
    }
}
