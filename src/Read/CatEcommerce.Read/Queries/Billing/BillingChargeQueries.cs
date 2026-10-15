using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Billing;

/// <summary>HO-BIL-001 §12.4 -- charges at the active branch. <see cref="Status"/> is <c>unbilled</c> (active and on
/// no live invoice), <c>billed</c> (on a live invoice) or <c>voided</c>; null lists all.</summary>
public sealed class GetChargesQuery : Query<PagedResult<ChargeViewModel>>
{
    public required Guid BranchId { get; init; }
    public Guid? CustomerId { get; init; }
    public string? Status { get; init; }
    public string? SourceKind { get; init; }
    public Guid? BillingGroupId { get; init; }
}

/// <summary>HO-BIL-001 §8.5 -- completed appointments (and from Phase J finalised visits, dispenses and completed
/// surgeries) with no charge, in the range, at the branch: the recoverable list when a capture failed.</summary>
public sealed class GetUncapturedSourcesQuery : Query<IReadOnlyList<UncapturedSourceViewModel>>
{
    public required Guid BranchId { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
}
