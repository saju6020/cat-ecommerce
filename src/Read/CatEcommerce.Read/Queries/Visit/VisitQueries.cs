using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Visit;

// HO-EMR-001 §12.6. A query never writes -- clinical-record access is logged by the filter's command.

/// <summary>The visit of the caller's active branch; another branch's visit is not found. The caller's roles
/// decide <c>allowedActions</c>; the caller's id decides which draft is returned.</summary>
public sealed class GetVisitDetailQuery : Query<VisitDetailViewModel?>
{
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
    public required IReadOnlyList<string> CallerRoles { get; init; }
}

public sealed class GetOpenVisitsQuery : Query<IReadOnlyList<VisitListItemViewModel>>
{
    public required Guid BranchId { get; init; }

    /// <summary><c>Open</c> (default) or <c>Finalised</c>.</summary>
    public string Status { get; init; } = "Open";

    /// <summary>A branch-local date; null means every date.</summary>
    public DateOnly? Date { get; init; }
}

public sealed class GetVisitByAppointmentQuery : Query<VisitReferenceViewModel>
{
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class GetVitalSignRulesQuery : Query<IReadOnlyList<VitalSignRuleViewModel>>
{
}
