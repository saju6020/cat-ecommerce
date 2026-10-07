namespace CatEcommerce.Domain.Models;

/// <summary>
/// The outcome of one <c>IRetentionPurgeSource.PurgeAsync</c> call (HO-XCT-001 §8.9, ADR-304).
/// Shape transcribed verbatim from §8.9's own code block.
/// </summary>
public sealed record PurgeResult(int Examined, int Purged, int Held, int Failed);
