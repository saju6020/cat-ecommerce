using CatEcommerce.Infrastructure.Billing.Documents;
using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Billing;

/// <summary>HO-BIL-001 §12.4 -- invoices at the active branch (staff).</summary>
public sealed class GetInvoicesQuery : Query<PagedResult<InvoiceSummaryViewModel>>
{
    public required Guid BranchId { get; init; }
    public Guid? CustomerId { get; init; }
    public string? Status { get; init; }
    public string? Number { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public string? BusinessLine { get; init; }

    /// <summary>The CSV export reads up to <c>BillingRules.MaxExportRows</c> instead of one page.</summary>
    public bool ForExport { get; init; }
}

/// <summary>Staff detail. Null for an unknown id or another branch's invoice (the controller answers <c>404</c>).</summary>
public sealed class GetInvoiceDetailQuery : Query<InvoiceDetailViewModel?>
{
    public required Guid InvoiceId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>The data for the invoice PDF (staff: branch-scoped; customer: <see cref="CustomerId"/> set, issued invoices only,
/// the caller or a customer merged into them).</summary>
public sealed class GetInvoiceDocumentQuery : Query<InvoiceDocument?>
{
    public required Guid InvoiceId { get; init; }
    public Guid? BranchId { get; init; }
    public Guid? CustomerId { get; init; }
}

/// <summary>"What would this invoice look like at today's rates?" -- writes nothing (§12.4).</summary>
public sealed class PreviewInvoiceQuery : Query<InvoicePreviewViewModel>
{
    public required Guid CustomerId { get; init; }
    public required Guid BranchId { get; init; }
    public required IReadOnlyList<Guid> ChargeIds { get; init; }
    public int? LoyaltyPoints { get; init; }
}

/// <summary>The customer's own issued invoices (§12.4) -- the caller and anyone merged into them; drafts and
/// discarded invoices are never visible.</summary>
public sealed class GetCustomerInvoicesQuery : Query<PagedResult<InvoiceSummaryViewModel>>
{
    public required Guid CustomerId { get; init; }
}

/// <summary>Null (-> <c>404</c>) when the invoice is not the caller's, or is not issued.</summary>
public sealed class GetCustomerInvoiceDetailQuery : Query<CustomerInvoiceDetailViewModel?>
{
    public required Guid CustomerId { get; init; }
    public required Guid InvoiceId { get; init; }
}
