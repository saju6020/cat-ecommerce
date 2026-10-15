using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Operations;

/// <summary>
/// HO-XCT-001 section 12.6 Q1 -- the offline front-desk snapshot of one branch for its local today. A pure read: issuing it is audited by the controller
/// through C10, never by the query (ADR-307).
/// </summary>
public sealed class GetFrontDeskOfflineSnapshotQuery : Query<FrontDeskSnapshotViewModel?>
{
    public required Guid BranchId { get; init; }
    public required IReadOnlyList<string> CallerRoles { get; init; }
    public required Guid StaffAccountId { get; init; }
}
