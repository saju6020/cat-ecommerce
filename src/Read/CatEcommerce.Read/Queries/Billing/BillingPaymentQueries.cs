using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Billing;

/// <summary>HO-BIL-001 §12.4 -- payments at the active branch (staff).</summary>
public sealed class GetPaymentsQuery : Query<PagedResult<PaymentSummaryViewModel>>
{
    public required Guid BranchId { get; init; }
    public Guid? CustomerId { get; init; }
    public string? Status { get; init; }
    public string? Method { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public bool ForExport { get; init; }
}

public sealed class GetPaymentDetailQuery : Query<PaymentDetailViewModel?>
{
    public required Guid PaymentId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>Staff: <see cref="BranchId"/> is the active branch. A customer: <see cref="CustomerId"/> instead (own accepted or reversed
/// payments only).</summary>
public sealed class GetPaymentReceiptQuery : Query<PaymentReceiptViewModel?>
{
    public required Guid PaymentId { get; init; }
    public Guid? BranchId { get; init; }
    public Guid? CustomerId { get; init; }
}

/// <summary>Pending payments at the active branch with the recorder's name and whether <b>the caller</b> may approve (never their own).</summary>
public sealed class GetPendingApprovalsQuery : Query<IReadOnlyList<PendingApprovalViewModel>>
{
    public required Guid BranchId { get; init; }
    public required Guid CallerId { get; init; }
}

/// <summary>The stored result of recording a payment -- used by the endpoint for a first answer and for an idempotent replay alike.</summary>
public sealed class GetPaymentRecordResultQuery : Query<PaymentRecordResultViewModel?>
{
    public required Guid PaymentId { get; init; }
}

/// <summary>A customer's own payments (§12.4): accepted and reversed ones only -- pending and rejected are never visible to a customer.</summary>
public sealed class GetCustomerPaymentsQuery : Query<PagedResult<PaymentSummaryViewModel>>
{
    public required Guid CustomerId { get; init; }
}

/// <summary>
/// The customer's money position (§12.4, US-BIL-03 AC1): outstanding, overdue, free credit (unallocated, non-earmarked), earmarked
/// credit, unbilled charges and the oldest due date. The customer set is the caller plus anyone merged into them (ADR-192).
/// <see cref="BranchId"/> only decides which zone "today" is in.
/// </summary>
public sealed class GetCustomerBillingSummaryQuery : Query<CustomerBillingSummaryViewModel>
{
    public required Guid CustomerId { get; init; }
    public Guid? BranchId { get; init; }
}

/// <summary>US-BIL-03 AC2 -- outstanding per issued invoice bucketed by days overdue, grouped by customer, at the active branch.</summary>
public sealed class GetAgeingQuery : Query<AgeingViewModel>
{
    public required Guid BranchId { get; init; }
    public DateOnly? AsOf { get; init; }
    public Guid? CustomerId { get; init; }
}

/// <summary>The published contract of §8.13: a stay's (or other group's) charges by business line, billed and unbilled, the invoiced
/// outstanding and its earmarked payments -- HO-BRD-001's spending limit (BR-8) and dashboard read it.</summary>
public sealed class GetBillingGroupSummaryQuery : Query<BillingGroupSummaryViewModel>
{
    public required string Kind { get; init; }
    public required Guid GroupId { get; init; }
}

/// <summary>The published contract of §8.13: what Billing knows about one source (an order, an appointment).</summary>
public sealed class GetSourceBillingStatusQuery : Query<SourceBillingStatusViewModel>
{
    public required string Kind { get; init; }
    public required Guid SourceId { get; init; }
}
