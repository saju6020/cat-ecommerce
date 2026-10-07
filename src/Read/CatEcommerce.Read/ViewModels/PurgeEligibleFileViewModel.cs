namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-FIL-001 §12 -- a read-only preview of what <c>IRetentionPurgeSource.PurgeAsync</c>
/// would purge for a given cutoff, without actually purging anything.</summary>
public sealed record PurgeEligibleFileViewModel(
    Guid FileId,
    string Purpose,
    long SizeInBytes,
    DateTimeOffset UploadedOn);
