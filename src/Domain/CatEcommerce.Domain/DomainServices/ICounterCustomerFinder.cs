namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 ADR-216 (rewritten, owner decision 6 of 2026-10-09) -- finds the customer a counter sale belongs to by the mobile number the cashier took.
/// Backed by HO-ACC-001's published lookup <c>FindCustomersQuery</c> (Q-H2): only an <b>Active</b> customer whose <b>primary</b> number matches counts
/// (an additional number is an unverified claim and never identifies a buyer). The adapter lives in the Api project because the Application layer does not
/// reference the Read project. Read-only.
/// </summary>
public interface ICounterCustomerFinder
{
    /// <summary>The active customer whose primary mobile is <paramref name="normalisedMobile"/>, or null.</summary>
    Task<CustomerContactFacts?> FindActiveByPrimaryMobileAsync(string normalisedMobile, CancellationToken ct);
}
