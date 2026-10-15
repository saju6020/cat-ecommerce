using CatEcommerce.Read.ViewModels;

namespace CatEcommerce.Read.ClinicalHistory;

/// <summary>
/// HO-EMR-001 §8.7 / ADR-128 -- the one extension point of the clinical history: each section is a provider,
/// so a later area (HO-PHM-001 prescriptions, HO-BRD-001 boarding stays, HO-ORD-001 food purchases) adds a panel
/// by registering a provider under the key <c>ClinicalHistorySections</c> already declares for it -- without
/// editing this slice. A declared section with no registered provider is reported as <c>available: false</c>
/// with its owning area.
///
/// Every provider reads with <c>AsNoTracking</c>, projects before materialising and is served by an index
/// (§12.1); none ever writes.
/// </summary>
public interface IClinicalHistorySectionProvider
{
    /// <summary>One of <c>ClinicalHistorySections</c>' keys: <c>visits</c>, <c>lab-results</c>, ...</summary>
    string Key { get; }

    Task<ClinicalHistorySectionSummary> GetSummaryAsync(Guid catId, CancellationToken ct);

    /// <summary>Newest first; <paramref name="pageNumber"/> is 1-based.</summary>
    Task<PagedResult<ClinicalHistoryItem>> GetPageAsync(Guid catId, int pageNumber, int pageSize, CancellationToken ct);
}

/// <summary>A section in the summary: whether it can be shown, which area owns it, and how many items it holds.</summary>
public sealed record ClinicalHistorySectionSummary(string Key, bool Available, string Owner, int Count);

/// <summary>
/// One item of a section. <see cref="OccurredOn"/> is an instant; an item known only by its date (a photograph's
/// <c>takenOn</c>, a vaccination's <c>administeredOn</c>) sits at 00:00 UTC of that date. Section-specific
/// attributes travel in <see cref="Fields"/> (text, never markup); a file is a signed, short-lived URL.
/// </summary>
public sealed record ClinicalHistoryItem(
    Guid Id,
    string Kind,
    DateTimeOffset OccurredOn,
    string Title,
    string? Detail,
    IReadOnlyDictionary<string, string?> Fields,
    StoredFileViewModel? File = null);
