using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Inventory;

/// <summary>HO-IMP-001 §12.4 (<c>Procurement.View</c>) -- paged consignments of the active branch.</summary>
public sealed class GetConsignmentsQuery : Query<PagedResult<ConsignmentSummaryViewModel>>
{
    public required Guid BranchId { get; init; }
    public string? Status { get; init; }
    public string? OriginCountryCode { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class GetConsignmentQuery : Query<ConsignmentDetailViewModel?>
{
    public required Guid ConsignmentId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class GetInspectionPackQuery : Query<InspectionPackViewModel?>
{
    public required Guid ConsignmentId { get; init; }
    public required Guid BranchId { get; init; }
}
