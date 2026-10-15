using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Operations;

/// <summary>
/// HO-XCT-001 section 11.1 C10 -- records that an offline front-desk snapshot was issued (NFR-SEC-8). One <c>BusinessEvent</c> audit row holding the
/// branch, the local date, the number of items per section and the ETag. No personal data: counts and ids only.
/// </summary>
public sealed class RecordOfflineSnapshotIssuedCommand : Command
{
    public required Guid BranchId { get; init; }
    public required DateOnly LocalDate { get; init; }

    /// <summary>Section key to number of items, for example <c>appointments</c> to 12.</summary>
    public required IReadOnlyDictionary<string, int> SectionCounts { get; init; }

    public required string ETag { get; init; }
}
