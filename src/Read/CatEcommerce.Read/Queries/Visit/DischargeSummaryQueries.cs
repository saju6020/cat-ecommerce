using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Visit;

// HO-EMR-001 §12.6 -- discharge summary and the customer (portal, FR-POR-3) reads. A non-finalised visit has no summary:
// null on every route, so it is a 404 for staff and customer alike (a draft is not a summary).

/// <summary>Staff: the visit must belong to the caller's active branch and be finalised.</summary>
public sealed class GetDischargeSummaryQuery : Query<DischargeSummaryViewModel?>
{
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>Customer: the visit's cat must be owned by <see cref="CustomerId"/> <b>now</b> (FR-CAT-8, so the previous owner
/// after a transfer gets nothing) and the visit finalised; anything else is <c>null</c> -- never a hint it exists.</summary>
public sealed class GetCustomerVisitSummaryQuery : Query<DischargeSummaryViewModel?>
{
    public required Guid VisitId { get; init; }
    public required Guid CustomerId { get; init; }
}

public sealed class GetCustomerCatVisitsQuery : Query<IReadOnlyList<CustomerVisitListItemViewModel>?>
{
    public required Guid CatId { get; init; }
    public required Guid CustomerId { get; init; }
}

public sealed class GetCustomerLabResultsQuery : Query<IReadOnlyList<CustomerLabResultViewModel>?>
{
    public required Guid CatId { get; init; }
    public required Guid CustomerId { get; init; }
}
