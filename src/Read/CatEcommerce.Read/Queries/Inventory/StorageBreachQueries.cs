using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Inventory;

public sealed class GetStorageBreachesQuery : Query<PagedResult<StorageBreachSummaryViewModel>>
{
    public required Guid BranchId { get; init; }
    public string? Status { get; init; }
}

public sealed class GetStorageBreachQuery : Query<StorageBreachDetailViewModel?>
{
    public required Guid BreachId { get; init; }
    public required Guid BranchId { get; init; }
}
