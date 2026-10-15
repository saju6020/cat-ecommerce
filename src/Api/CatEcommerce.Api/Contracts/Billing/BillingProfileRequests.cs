using CatEcommerce.Domain.Models;

namespace CatEcommerce.Api.Contracts.Billing;

/// <summary>HO-BIL-001 §12.4 -- <c>PUT /api/admin/billing-profiles/{branchId}</c> (C1).</summary>
public sealed class SetBillingProfileRequest
{
    public string LegalName { get; init; } = default!;
    public string TaxRegistrationNumber { get; init; } = default!;
    public IReadOnlyList<string> AddressLines { get; init; } = [];
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? InvoiceFooter { get; init; }
    public IReadOnlyList<BankAccountInput> BankAccounts { get; init; } = [];
}
