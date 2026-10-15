using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>A customer as an order needs them: the name and the normalised mobile number, and whether the account may buy.</summary>
public sealed record CustomerContactFacts(Guid CustomerId, string FullName, string? Mobile, bool IsActive);

/// <summary>
/// HO-ORD-001 §8.14 -- the read port over HO-ACC-001's <c>account.*</c> tables: a customer's name, mobile and status, and a delivery address from their
/// address book. <c>AsNoTracking</c> projections; Ordering never loads the account or the address book aggregate. Implemented in Infrastructure.
/// </summary>
public interface ICustomerDirectory
{
    Task<CustomerContactFacts?> GetCustomerAsync(Guid customerId, CancellationToken ct);

    /// <summary>The address, only if it is in <b>this customer's</b> address book (another customer's address id finds nothing).</summary>
    Task<AddressFacts?> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken ct);
}
