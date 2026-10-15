using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Visit;

// HO-EMR-001 §12.6 -- diagnostic reads. Branch-scoped: another branch's order is not found.

public sealed class GetDiagnosticOrderQuery : Query<DiagnosticOrderViewModel?>
{
    public required Guid DiagnosticOrderId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class GetPendingDiagnosticsQuery : Query<IReadOnlyList<DiagnosticOrderListItemViewModel>>
{
    public required Guid BranchId { get; init; }

    /// <summary><c>Pending</c> (default), <c>Resulted</c> or <c>Cancelled</c>.</summary>
    public string Status { get; init; } = "Pending";
}
