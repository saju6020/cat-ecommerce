using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Order;

/// <summary>A return of the caller's (null -> <c>404</c>, §12.3).</summary>
public sealed class GetCustomerReturnQuery : Query<CustomerReturnViewModel?>
{
    public required Guid CustomerId { get; init; }
    public required Guid ReturnRequestId { get; init; }
}

/// <summary>Staff: the returns of the active branch's orders, newest first, optionally one status.</summary>
public sealed class GetReturnsQuery : Query<PagedResult<StaffReturnRowViewModel>>
{
    public required Guid BranchId { get; init; }
    public ReturnStatus? Status { get; init; }
}

public sealed class GetReturnDetailQuery : Query<StaffReturnDetailViewModel?>
{
    public required Guid BranchId { get; init; }
    public required Guid ReturnRequestId { get; init; }
}

/// <summary>Null (-> <c>404</c>) when the order is not found at the branch.</summary>
public sealed class GetRefundOptionsQuery : Query<RefundOptionsViewModel?>
{
    public required Guid BranchId { get; init; }
    public required Guid OrderId { get; init; }
}
