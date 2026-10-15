using CatEcommerce.Domain.Models;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Billing;

/// <summary>HO-BIL-001 §11.1 C1 (NFR-LEG-3) -- the seller's legal identity at one branch. Data only; the
/// profile's id is the branch id (created on first write).</summary>
public sealed class SetBillingProfileCommand : Command
{
    public required Guid BranchId { get; init; }
    public required string LegalName { get; init; }
    public required string TaxRegistrationNumber { get; init; }
    public required IReadOnlyList<string> AddressLines { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? InvoiceFooter { get; init; }
    public required IReadOnlyList<BankAccountInput> BankAccounts { get; init; }
}
