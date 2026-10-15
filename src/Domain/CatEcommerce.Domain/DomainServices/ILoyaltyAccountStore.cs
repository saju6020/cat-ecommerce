using CatEcommerce.Domain.Aggregates.Billing;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BIL-001 section 8.11 -- a customer loyalty account, created on first use. The account row is inserted outside the caller unit of work
/// (<c>INSERT ... ON CONFLICT DO NOTHING</c>, a row with a zero balance changes nothing) and returned <b>tracked</b>, so two requests that both
/// need a first account never collide on its primary key and the posting that follows is one save of the account and its ledger entry.
/// </summary>
public interface ILoyaltyAccountStore
{
    Task<LoyaltyAccountAggregate> EnsureAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Adds a ledger entry to the unit of work <b>without saving</b>, so a posting that writes two entries (a merge transfer) commits both with the
    /// accounts in one save (ADR-184).</summary>
    void Stage(LoyaltyEntryAggregate entry);
}
