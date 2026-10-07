using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.StoredFile;

/// <summary>HO-FIL-001 §12: a read-only preview mirroring <c>StoredFilePurgeSource</c>'s own
/// eligibility rule (§8.7) -- excludes <c>LegalHold</c> and any row whose <c>RetainUntil</c> is in
/// the future -- without purging anything.</summary>
public sealed class GetFilesForPurgeQuery : Query<IReadOnlyList<PurgeEligibleFileViewModel>>
{
    public required DateTimeOffset Cutoff { get; init; }
}
