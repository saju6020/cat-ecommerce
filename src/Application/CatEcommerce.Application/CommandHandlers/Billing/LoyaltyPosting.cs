using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Billing;

/// <summary>What the loyalty postings need, resolved once by DI: the account store (create on first use) and the repositories of the account, the
/// ledger entry and the invoice that a posting commits together (ADR-184).</summary>
public sealed record LoyaltyDependencies(
    ILoyaltyAccountStore Store,
    IAggregateRootRepository<LoyaltyAccountAggregate> Accounts,
    IAggregateRootRepository<LoyaltyEntryAggregate> Entries,
    IAggregateRootRepository<InvoiceAggregate> Invoices);

/// <summary>
/// HO-BIL-001 section 8.11 (ADR-191, ADR-184) -- the points postings shared by the commands and the event handlers: account + ledger entry (+ the draft
/// invoice where relevant) in <b>one</b> save. A static helper over repositories, not a service layer. Never enqueues a message.
/// </summary>
public static class LoyaltyPosting
{
    /// <summary>A redemption leaves a draft (removed, discarded, or no longer fitting): the points go back to the customer, the invoice change is
    /// committed in the same save.</summary>
    public static async Task ReverseRedemptionAsync(LoyaltyDependencies d, InvoiceAggregate invoice, LoyaltyRedemption redemption, DateTimeOffset now, BillingActor actor)
    {
        var account = await d.Store.EnsureAsync(invoice.CustomerId);
        account.ReverseRedemption(redemption.Points);

        var entry = new LoyaltyEntryAggregate(Guid.NewGuid());
        entry.Record(new LoyaltyEntryDto(invoice.CustomerId, LoyaltyEntryKind.RedemptionReversal, redemption.Points, invoice.Id, null, null, 0, now, actor));

        await BillingPosting.CommitNewAsync(d.Entries, entry, BillingPosting.Of(d.Accounts, account), BillingPosting.Of(d.Invoices, invoice));
    }
}
