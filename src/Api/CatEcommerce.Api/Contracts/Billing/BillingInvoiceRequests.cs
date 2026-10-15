using CatEcommerce.Application.Commands.Billing;

namespace CatEcommerce.Api.Contracts.Billing;

/// <summary><c>POST /api/staff/billing/invoices</c> (C5).</summary>
public sealed class CreateDraftInvoiceRequest
{
    public Guid CustomerId { get; init; }
    public IReadOnlyList<Guid> ChargeIds { get; init; } = [];
    public string? Notes { get; init; }
}

/// <summary><c>PUT /api/staff/billing/invoices/{id}/charges</c> (C6).</summary>
public sealed class SetDraftInvoiceChargesRequest
{
    public int ExpectedVersion { get; init; }
    public IReadOnlyList<Guid> ChargeIds { get; init; } = [];
}

/// <summary><c>POST /api/staff/billing/invoices/{id}/issue</c> (C8).</summary>
public sealed class IssueInvoiceRequest
{
    public int ExpectedVersion { get; init; }
}

/// <summary>Exactly one of <c>chargeIds</c>, <c>source</c> or <c>billingGroup</c> (§8.13).</summary>
public sealed class InvoiceChargeSelectorRequest
{
    public IReadOnlyList<Guid>? ChargeIds { get; init; }
    public SourceChargeSelector? Source { get; init; }
    public BillingGroupChargeSelector? BillingGroup { get; init; }

    public InvoiceChargeSelector ToSelector() => new(ChargeIds, Source, BillingGroup);
}

/// <summary><c>POST /api/staff/billing/invoices/issue-from-charges</c> (C9).</summary>
public sealed class IssueInvoiceFromChargesRequest
{
    public Guid CustomerId { get; init; }
    public InvoiceChargeSelectorRequest Selector { get; init; } = new();
}

/// <summary><c>POST /api/staff/billing/invoices/preview</c> -- writes nothing.</summary>
public sealed class PreviewInvoiceRequest
{
    public Guid CustomerId { get; init; }
    public IReadOnlyList<Guid> ChargeIds { get; init; } = [];
    public int? LoyaltyPoints { get; init; }
}
