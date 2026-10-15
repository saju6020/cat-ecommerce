using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Order;

/// <summary>HO-ORD-001 §12.4 -- the staff order queue of the active branch, newest first. <c>Search</c> matches the order number, the recipient's name and phone.</summary>
public sealed class GetStaffOrderQueueQuery : Query<PagedResult<StaffOrderRowViewModel>>
{
    public required Guid BranchId { get; init; }
    public OrderStatus? Status { get; init; }
    public FulfilmentMethod? Fulfilment { get; init; }
    public OrderPaymentMethod? PaymentMethod { get; init; }
    public OrderChannel? Channel { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? Search { get; init; }
}

/// <summary>Null (-> <c>404</c>) when the order is not found at the branch.</summary>
public sealed class GetStaffOrderDetailQuery : Query<StaffOrderDetailViewModel?>
{
    public required Guid BranchId { get; init; }
    public required Guid OrderId { get; init; }
}

/// <summary>US-ORD-10 AC1 -- grouped by location, then batch, quantities per order. With no ids: every <c>Confirmed</c> and <c>Packing</c> order of the branch.</summary>
public sealed class GetPickListQuery : Query<PickListViewModel>
{
    public required Guid BranchId { get; init; }
    public IReadOnlyList<Guid>? OrderIds { get; init; }
}

/// <summary>US-ORD-10 AC2. Null (-> <c>404</c>) when the order is not found at the branch.</summary>
public sealed class GetPackingSlipQuery : Query<PackingSlipViewModel?>
{
    public required Guid BranchId { get; init; }
    public required Guid OrderId { get; init; }
}

/// <summary>US-ORD-11 AC2 -- the orders dispatched with one courier on one branch-local date (the CSV manifest).</summary>
public sealed class GetCourierManifestQuery : Query<IReadOnlyList<ManifestRowViewModel>>
{
    public required Guid BranchId { get; init; }
    public required Guid CourierId { get; init; }
    public required DateOnly Date { get; init; }
}

/// <summary>HO-ORD-001 §12.4 -- one customer's orders at the staff member's active branch, newest first (HO-ORD-002 §11.14, the customer page panel).
/// Null (-> <c>404</c>) when no such customer account exists.</summary>
public sealed class GetStaffCustomerOrdersQuery : Query<PagedResult<StaffCustomerOrderRowViewModel>?>
{
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
}
