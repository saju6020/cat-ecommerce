using CatEcommerce.Application.Commands.Billing;

namespace CatEcommerce.Api.Contracts.Billing;

/// <summary><c>POST /api/staff/billing/payments</c> (C10). <c>method</c> is a payment-method name from
/// <c>GET /api/billing/payment-methods</c>; the reference and collector fields required for it come from that table's flags.</summary>
public sealed class RecordPaymentRequest
{
    public Guid CustomerId { get; init; }
    public string Method { get; init; } = default!;
    public decimal Amount { get; init; }
    public DateOnly ReceivedOn { get; init; }
    public string? ExternalReference { get; init; }
    public PaymentCollectorInput? Collector { get; init; }
    public PaymentEarmarkInput? Earmark { get; init; }
    public IReadOnlyList<PaymentAllocationInput> Allocations { get; init; } = [];
    public string? Notes { get; init; }
}

public sealed class ReasonRequest
{
    public string Reason { get; init; } = default!;
}

public sealed class AllocatePaymentRequest
{
    public IReadOnlyList<PaymentAllocationInput> Allocations { get; init; } = [];
}
