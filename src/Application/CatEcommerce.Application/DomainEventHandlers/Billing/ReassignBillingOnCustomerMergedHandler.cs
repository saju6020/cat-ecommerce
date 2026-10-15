using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Billing;

/// <summary>
/// HO-BIL-001 §11.6 / ADR-192 -- HO-ACC-001's <c>CustomerMergedEvent</c> is binding on every context that holds a
/// customer id (ADR-039). <b>Mutable</b> records move: the duplicate's unbilled active charges and its draft invoices
/// go to the survivor (one <c>UpdateAsync</c> each, idempotent -- each aggregate's own <c>ReassignOwner</c> is a no-op
/// once moved). <b>Immutable</b> financial records -- issued invoices, payments, credit notes, refunds -- keep the
/// original customer id (rewriting an issued document would break ADR-180); the customer-facing and summary reads fold
/// the merged customer in instead. Never enqueues a message.
/// </summary>
public sealed class ReassignBillingOnCustomerMergedHandler(
    IBillingFinder finder,
    IAggregateRootRepository<ChargeAggregate> chargeRepository,
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    LoyaltyDependencies loyalty,
    TimeProvider timeProvider)
    : IEventHandlerAsync<CustomerMergedEvent>
{
    public async Task HandleAsync(CustomerMergedEvent @event)
    {
        foreach (var chargeId in await finder.FindMovableChargeIdsAsync(@event.DuplicateCustomerId))
        {
            var charge = await chargeRepository.GetByIdAsync(chargeId);
            if (charge is null) continue;

            charge.ReassignOwner(@event.SurvivingCustomerId);
            await chargeRepository.UpdateAsync(charge, EventPublishOption.InMemoryPublishOnly);
        }

        foreach (var invoiceId in await finder.FindDraftInvoiceIdsAsync(@event.DuplicateCustomerId))
        {
            var invoice = await invoiceRepository.GetByIdAsync(invoiceId);
            if (invoice is null) continue;

            invoice.ReassignOwner(@event.SurvivingCustomerId);
            await invoiceRepository.UpdateAsync(invoice, EventPublishOption.InMemoryPublishOnly);
        }

        await MoveLoyaltyBalanceAsync(@event);
    }

    /// <summary>The duplicate's whole points balance goes to the survivor as a <c>MergeTransferOut</c> and a <c>MergeTransferIn</c> entry, both keyed by
    /// the duplicate, so a re-delivered event moves nothing twice (ADR-192).</summary>
    private async Task MoveLoyaltyBalanceAsync(CustomerMergedEvent @event)
    {
        var duplicate = await loyalty.Accounts.GetByIdAsync(@event.DuplicateCustomerId);
        if (duplicate is null || duplicate.Balance <= 0) return;
        if (await finder.LoyaltyEntryExistsAsync(LoyaltyEntryKind.MergeTransferOut, null, null, @event.DuplicateCustomerId)) return;

        var actor = new BillingActor(@event.UserContext!, @event.CorrelationId);
        var now = timeProvider.GetUtcNow();
        var survivor = await loyalty.Store.EnsureAsync(@event.SurvivingCustomerId);

        var moved = duplicate.TransferOut();
        survivor.TransferIn(moved);

        var outEntry = new LoyaltyEntryAggregate(Guid.NewGuid());
        outEntry.Record(new LoyaltyEntryDto(@event.DuplicateCustomerId, LoyaltyEntryKind.MergeTransferOut, moved, null, null, @event.DuplicateCustomerId, 0, now, actor));
        var inEntry = new LoyaltyEntryAggregate(Guid.NewGuid());
        inEntry.Record(new LoyaltyEntryDto(@event.SurvivingCustomerId, LoyaltyEntryKind.MergeTransferIn, moved, null, null, @event.DuplicateCustomerId, 0, now, actor));

        // ONE save: both accounts and both ledger legs commit together (ADR-184). A redelivered event finds the Out entry (the first fact written)
        // and does nothing; there is no state in which a balance has moved without its entries.
        loyalty.Store.Stage(inEntry);
        await BillingPosting.CommitNewAsync(loyalty.Entries, outEntry, BillingPosting.Of(loyalty.Accounts, duplicate), BillingPosting.Of(loyalty.Accounts, survivor));
    }
}
