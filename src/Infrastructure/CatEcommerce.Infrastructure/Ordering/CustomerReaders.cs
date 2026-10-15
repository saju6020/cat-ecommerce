using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>HO-ORD-001 §8.14 -- <see cref="ICustomerDirectory"/> over <c>account.*</c>: <c>AsNoTracking</c> projections of a customer's contact facts and of
/// one address from <b>that customer's</b> address book (the address book's id is the customer's id, so a stranger's address id finds nothing).</summary>
public sealed class CustomerDirectory(CatEcommerceDbContext db) : ICustomerDirectory
{
    public async Task<CustomerContactFacts?> GetCustomerAsync(Guid customerId, CancellationToken ct)
    {
        var row = await db.Set<CustomerAccountAggregate>().AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new { c.Id, c.FullName, Mobile = c.MobileNumber != null ? c.MobileNumber.Value : null, c.Status })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : new CustomerContactFacts(row.Id, row.FullName, row.Mobile, row.Status == CustomerAccountStatus.Active);
    }

    public async Task<AddressFacts?> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken ct) =>
        await db.Set<CustomerAddressBookAggregate>().AsNoTracking()
            .Where(b => b.Id == customerId)
            .SelectMany(b => b.Addresses)
            .Where(a => a.Id == addressId)
            .Select(a => new AddressFacts(a.Id, a.Line1, a.Line2, a.City, a.District, a.PostalCode, a.DeliveryInstructions))
            .FirstOrDefaultAsync(ct);
}

/// <summary>HO-ORD-001 §8.7 -- <see cref="ICustomerPolicyStatus"/> over <c>admin.policy_*</c>: the current <c>RequiresAcceptance</c> documents this customer has not accepted
/// at their current version. One query with a correlated <c>NOT EXISTS</c>; nothing is written.</summary>
public sealed class CustomerPolicyStatus(CatEcommerceDbContext db) : ICustomerPolicyStatus
{
    public async Task<IReadOnlyList<PendingPolicyFact>> GetPendingAsync(Guid customerId, CancellationToken ct)
    {
        var rows = await db.Set<PolicyDocumentAggregate>().AsNoTracking()
            .Where(d => d.RequiresAcceptance && d.CurrentVersionNumber != null)
            .Where(d => !db.Set<PolicyAcceptanceAggregate>().Any(a =>
                a.CustomerAccountId == customerId && a.PolicyKey == d.Key && a.VersionNumber == d.CurrentVersionNumber))
            .OrderBy(d => d.Key)
            .Select(d => new { d.Key, VersionNumber = d.CurrentVersionNumber!.Value, d.Title })
            .ToListAsync(ct);

        return rows.Select(r => new PendingPolicyFact(r.Key, r.VersionNumber, r.Title)).ToList();
    }
}
