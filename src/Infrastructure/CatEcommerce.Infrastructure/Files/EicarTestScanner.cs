using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using Microsoft.Extensions.Hosting;

namespace CatEcommerce.Infrastructure.Files;

/// <summary>
/// HO-FIL-001 §8.5: the Testing-environment scanner. Deterministic and offline (no <c>clamd</c>
/// container required to run the automated suite, §20.3) -- it recognises the standard EICAR
/// antivirus test string (the same 68-byte string every real antivirus engine, including
/// ClamAV, is designed to flag) and returns <see cref="ScanOutcome.Infected"/> for it, else
/// <see cref="ScanOutcome.Clean"/>. Real end-to-end scanning against a genuine <c>clamd</c> is
/// exercised separately for <see cref="ClamAvScanner"/> itself (V3), not substituted here.
/// The constructor throws outside Testing, mirroring <see cref="DevelopmentNoOpScanner"/>'s own
/// guard, so a wiring mistake cannot let this deterministic stand-in reach a real environment.
///
/// Gate-fix 2026-09-27 (SECURITY LOW, HO-FIL-001-SECURITY.md): scans the WHOLE stream with a
/// rolling window, not just the first 4096 bytes -- previously an EICAR string placed after 5000
/// bytes of leading text was accepted and stored, which meant I2 ("infected content never reaches
/// the bucket") was only proven for payloads at the very start of the file.
/// </summary>
public sealed class EicarTestScanner : IMalwareScanner
{
    /// <summary>The standard EICAR test string (European Institute for Computer Antivirus
    /// Research) -- every antivirus product recognises it without it being an actual virus.</summary>
    public const string EicarSignature = @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";

    private static readonly byte[] EicarBytes = System.Text.Encoding.ASCII.GetBytes(EicarSignature);
    private const int ChunkSize = 81920;

    public EicarTestScanner(IHostEnvironment environment)
    {
        if (environment.EnvironmentName != "Testing")
            throw new InvalidOperationException(
                $"{nameof(EicarTestScanner)} must never be constructed outside Testing (ADR-319).");
    }

    public async Task<ScanResult> ScanAsync(Stream content, CancellationToken ct)
    {
        if (content.CanSeek) content.Position = 0;

        // A rolling window: keep the tail of the previous chunk (one byte short of the whole
        // signature) prefixed onto the next chunk, so a signature split across a chunk boundary is
        // still found.
        var overlapLength = EicarBytes.Length - 1;
        var window = new byte[overlapLength + ChunkSize];
        var windowFilled = 0;
        var infected = false;

        var buffer = new byte[ChunkSize];
        int read;
        while (!infected && (read = await content.ReadAsync(buffer, ct)) > 0)
        {
            Buffer.BlockCopy(buffer, 0, window, windowFilled, read);
            windowFilled += read;

            if (IndexOf(window.AsSpan(0, windowFilled), EicarBytes) >= 0)
            {
                infected = true;
                break;
            }

            // Slide: keep only the trailing `overlapLength` bytes for the next iteration.
            var keep = Math.Min(overlapLength, windowFilled);
            Buffer.BlockCopy(window, windowFilled - keep, window, 0, keep);
            windowFilled = keep;
        }

        if (content.CanSeek) content.Position = 0;

        return infected
            ? new ScanResult(ScanOutcome.Infected, "EicarTest Eicar-Signature")
            : new ScanResult(ScanOutcome.Clean, "EicarTest");
    }

    private static int IndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.Slice(i, needle.Length).SequenceEqual(needle)) return i;
        }

        return -1;
    }
}
