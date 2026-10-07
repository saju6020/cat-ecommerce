namespace CatEcommerce.Domain.Models;

/// <summary>
/// <c>Clean</c> / <c>Infected</c> / <c>Unavailable</c> -- the contract HO-EMR-001 §8.10 defined,
/// carried into HO-FIL-001 (ADR-319). A transient discriminator returned by a read-only port, never
/// persisted on an aggregate, so a plain C# enum rather than <see cref="Enumeration"/>
/// (.ai/enumeration-pattern.md: "internal-only flags... that carry no behaviour").
/// </summary>
public enum ScanOutcome
{
    Clean,
    Infected,
    Unavailable
}

/// <summary>
/// <c>IMalwareScanner.ScanAsync</c>'s result. <see cref="ScannerName"/> mirrors
/// <c>StoredFileAggregate.ScannerName</c> (HO-FIL-001 §8.1) -- "which scanner passed it, so a later
/// signature recall can find what was scanned by what". No handover writes the method's exact
/// signature; inferred from §8.2 step 5 and ADR-319. Flagged as inferred in
/// docs/handover/CONTRACTS.md.
/// </summary>
public sealed record ScanResult(ScanOutcome Outcome, string? ScannerName);
