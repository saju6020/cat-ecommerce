using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.7, §8.14 (US-ADM-07 AC2, checkout half) -- which current <c>RequiresAcceptance</c> policies a customer has not yet accepted, read over
/// HO-ADM-001's <c>admin.policy_*</c> tables. Ordering <b>checks</b> acceptance and never writes it: the client accepts through HO-ADM-001's endpoint
/// first. Implemented in Infrastructure.
/// </summary>
public interface ICustomerPolicyStatus
{
    Task<IReadOnlyList<PendingPolicyFact>> GetPendingAsync(Guid customerId, CancellationToken ct);
}
