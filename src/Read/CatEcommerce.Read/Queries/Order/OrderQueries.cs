using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Order;

/// <summary>HO-ORD-001 §12.6 -- the signed-in customer's orders, newest first, optionally one status. Paged.</summary>
public sealed class GetCustomerOrdersQuery : Query<PagedResult<CustomerOrderSummaryViewModel>>
{
    public required Guid CustomerId { get; init; }
    public OrderStatus? Status { get; init; }
}

/// <summary>One order of the caller's (null -> <c>404</c>: the same answer for an order that is someone else's, §12.3).</summary>
public sealed class GetCustomerOrderDetailQuery : Query<CustomerOrderDetailViewModel?>
{
    public required Guid CustomerId { get; init; }
    public required Guid OrderId { get; init; }
}

/// <summary>Staff: the bank-transfer orders of the active branch still waiting for money, with any confirmation waiting for a second person.</summary>
public sealed class GetAwaitingPaymentOrdersQuery : Query<IReadOnlyList<AwaitingPaymentOrderViewModel>>
{
    public required Guid BranchId { get; init; }
}

/// <summary>Staff (accountants): the reconciliation issues, newest first, optionally one status.</summary>
public sealed class GetReconciliationIssuesQuery : Query<PagedResult<ReconciliationIssueViewModel>>
{
    public required Guid BranchId { get; init; }
    public ReconciliationIssueStatus? Status { get; init; }
}

/// <summary>Staff: orders with money owed back to the customer (<c>RefundDue &gt; 0</c>) at the active branch.</summary>
public sealed class GetRefundsDueQuery : Query<PagedResult<RefundDueOrderViewModel>>
{
    public required Guid BranchId { get; init; }
}

/// <summary>Worker: ids of unpaid bank-transfer orders past their window and holding no confirmation, oldest first, at most <see cref="Take"/>.</summary>
public sealed class GetExpirableOrdersQuery : Query<IReadOnlyList<Guid>>
{
    public int Take { get; init; } = 200;

    /// <summary>Tests only: restrict the pass to one branch so a test's clock jump never expires another test's orders in the shared database. Null in production.</summary>
    public Guid? BranchId { get; init; }
}
