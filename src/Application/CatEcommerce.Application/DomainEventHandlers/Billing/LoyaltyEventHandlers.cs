using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Billing;

/// <summary>
/// HO-BIL-001 section 8.11 / 11.6 (US-BIL-10 AC1, ADR-191) -- points accrue when an invoice becomes <c>Paid</c>: <c>floor(qualifying net / spend per point)</c>, the
/// qualifying net being the frozen net of every line that is not a fee (after all discounts, before tax) -- across clinic, shop and boarding alike.
/// One posting (account + <c>Accrual</c> entry); a no-op when loyalty is off; idempotent by the invoice (a second delivery of the event finds the
/// entry). It mutates Billing roots only and <b>never enqueues</b>. Credit notes issued before the invoice settled are applied at once, so the order
/// the two events arrive in cannot leave points the customer should not have.
/// </summary>
public sealed class AccrueLoyaltyOnInvoicePaidHandler(
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    LoyaltyDependencies loyalty,
    IBillingFinder finder,
    IBusinessSettings settings,
    ReverseLoyaltyOnCreditNoteIssuedHandler reversal,
    TimeProvider timeProvider)
    : IEventHandlerAsync<InvoiceStatusChangedEvent>
{
    public async Task HandleAsync(InvoiceStatusChangedEvent @event)
    {
        if (@event.To != InvoiceStatus.Paid || !settings.GetBool(SettingKeys.BillingLoyaltyEnabled)) return;

        var invoice = await invoiceRepository.GetByIdAsync(@event.InvoiceId);
        if (invoice is null) return;

        if (await finder.LoyaltyEntryExistsAsync(LoyaltyEntryKind.Accrual, invoice.Id, null, null)) return;

        var qualifyingNet = invoice.Lines.Where(l => l.ItemKind != ItemKind.Fee).Sum(l => l.NetAmount);
        var points = LoyaltyCalculator.AccrualPoints(qualifyingNet, settings.GetDecimal(SettingKeys.BillingLoyaltySpendPerPoint));
        if (points <= 0) return;

        var customer = await customerRepository.GetByIdAsync(invoice.CustomerId);
        var owner = customer?.MergedIntoCustomerId ?? invoice.CustomerId;
        var actor = new BillingActor(@event.UserContext!, @event.CorrelationId);

        var account = await loyalty.Store.EnsureAsync(owner);
        account.Accrue(points);

        var entry = new LoyaltyEntryAggregate(Guid.NewGuid());
        entry.Record(new LoyaltyEntryDto(owner, LoyaltyEntryKind.Accrual, points, invoice.Id, null, null, 0, timeProvider.GetUtcNow(), actor));
        await BillingPosting.CommitNewAsync(loyalty.Entries, entry, BillingPosting.Of(loyalty.Accounts, account));

        foreach (var creditNoteId in await finder.FindCreditNoteIdsAsync(invoice.Id))
            await reversal.ReverseForAsync(creditNoteId, @event.UserContext!, @event.CorrelationId);
    }
}

/// <summary>
/// HO-BIL-001 section 8.11 -- a credit note takes back the points its <b>qualifying net</b> earned: <c>floor(credited qualifying net / spend per point)</c>, never more than
/// the invoice still has accrued. When the customer has already spent them the balance goes to zero and the difference is recorded on the entry as
/// <c>Shortfall</c>. Idempotent by the credit note; a no-op when loyalty is off or the invoice accrued nothing. Never enqueues.
/// </summary>
public sealed class ReverseLoyaltyOnCreditNoteIssuedHandler(
    IAggregateRootRepository<CreditNoteAggregate> creditNoteRepository,
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    LoyaltyDependencies loyalty,
    IBillingFinder finder,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : IEventHandlerAsync<CreditNoteIssuedEvent>
{
    public Task HandleAsync(CreditNoteIssuedEvent @event) => ReverseForAsync(@event.CreditNoteId, @event.UserContext!, @event.CorrelationId);

    public async Task ReverseForAsync(Guid creditNoteId, UserContext userContext, Guid correlationId)
    {
        if (!settings.GetBool(SettingKeys.BillingLoyaltyEnabled)) return;
        if (await finder.LoyaltyEntryExistsAsync(LoyaltyEntryKind.AccrualReversal, null, creditNoteId, null)) return;

        var creditNote = await creditNoteRepository.GetByIdAsync(creditNoteId);
        var invoice = creditNote is null ? null : await invoiceRepository.GetByIdAsync(creditNote.InvoiceId);
        if (creditNote is null || invoice is null) return;

        var qualifying = invoice.Lines.Where(l => l.ItemKind != ItemKind.Fee).Select(l => l.Id).ToHashSet();
        var creditedNet = creditNote.Lines.Where(l => qualifying.Contains(l.InvoiceLineId)).Sum(l => l.NetAmount);

        var points = LoyaltyCalculator.ReversalPoints(
            creditedNet, settings.GetDecimal(SettingKeys.BillingLoyaltySpendPerPoint), await finder.GetAccruedNotReversedAsync(invoice.Id));
        if (points <= 0) return;

        var customer = await customerRepository.GetByIdAsync(invoice.CustomerId);
        var owner = customer?.MergedIntoCustomerId ?? invoice.CustomerId;
        var account = await loyalty.Store.EnsureAsync(owner);
        var shortfall = account.ReverseAccrual(points);

        var entry = new LoyaltyEntryAggregate(Guid.NewGuid());
        entry.Record(new LoyaltyEntryDto(owner, LoyaltyEntryKind.AccrualReversal, points, null, creditNote.Id, null, shortfall, timeProvider.GetUtcNow(), new BillingActor(userContext, correlationId)));
        await BillingPosting.CommitNewAsync(loyalty.Entries, entry, BillingPosting.Of(loyalty.Accounts, account));
    }
}
