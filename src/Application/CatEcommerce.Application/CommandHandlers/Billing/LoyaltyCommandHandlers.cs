using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Billing.BillingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Billing;

/// <summary>C13's result: what was drawn from which payment, what free credit the customer still has, and what the invoice still owes.</summary>
public sealed record AppliedCustomerCreditResult(IReadOnlyList<AppliedAllocation> Applied, decimal FreeCreditRemaining, decimal InvoiceOutstanding);

/// <summary>
/// HO-BIL-001 section 11.4 C27 (US-BIL-10 AC2, ADR-191) -- redeem points on a <b>draft</b>: loyalty enabled, at least the configured minimum, no more than the
/// balance, worth no more than the net of the lines that earn points. One posting: the draft (<c>LoyaltyRedemption</c>), the account and a
/// <c>Redemption</c> entry. The discount is apportioned across the qualifying lines when the invoice is issued.
/// </summary>
public sealed class ApplyLoyaltyRedemptionCommandHandler(
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    LoyaltyDependencies loyalty,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ApplyLoyaltyRedemptionCommand>
{
    public async Task<CommandResponse> HandleAsync(ApplyLoyaltyRedemptionCommand command)
    {
        if (!settings.GetBool(SettingKeys.BillingLoyaltyEnabled))
            return Violation(BusinessViolationCodes.LoyaltyNotEnabled, nameof(command.Points), "Loyalty points are not switched on.");

        var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId);
        if (invoice is null || invoice.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.InvoiceNotFound, nameof(command.InvoiceId), "Invoice not found.");

        if (invoice.Status != InvoiceStatus.Draft)
            return Violation(BusinessViolationCodes.InvoiceNotDraft, nameof(command.InvoiceId), BillingMessages.InvoiceNotDraft);

        var minimum = settings.GetInt(SettingKeys.BillingLoyaltyMinimumRedemptionPoints);
        if (command.Points < minimum)
            return Violation(BusinessViolationCodes.LoyaltyBelowMinimum, nameof(command.Points), $"At least {minimum} points must be redeemed at a time.");

        var account = await loyalty.Store.EnsureAsync(invoice.CustomerId);
        if (command.Points > account.Balance)
            return Violation(BusinessViolationCodes.LoyaltyInsufficientPoints, nameof(command.Points), $"The customer has {account.Balance} points; {command.Points} were asked for.");

        var actor = command.ToBillingActor();
        var now = timeProvider.GetUtcNow();
        var entry = new LoyaltyEntryAggregate(Guid.NewGuid());
        try
        {
            var value = LoyaltyCalculator.RedemptionValue(command.Points, settings.GetDecimal(SettingKeys.BillingLoyaltyPointValue));
            invoice.ApplyLoyaltyRedemption(new LoyaltyRedemptionDto(command.Points, value, entry.Id, 0m), actor);
            account.Redeem(command.Points);
            entry.Record(new LoyaltyEntryDto(invoice.CustomerId, LoyaltyEntryKind.Redemption, command.Points, invoice.Id, null, null, 0, now, actor));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await BillingPosting.CommitNewAsync(loyalty.Entries, entry, BillingPosting.Of(loyalty.Accounts, account), BillingPosting.Of(loyalty.Invoices, invoice));
        return Success(invoice.Id);
    }
}

/// <summary>C27 -- take a redemption off a draft: the points go back to the customer in the same save. Removing what is not there is a no-op.</summary>
public sealed class RemoveLoyaltyRedemptionCommandHandler(
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    LoyaltyDependencies loyalty,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RemoveLoyaltyRedemptionCommand>
{
    public async Task<CommandResponse> HandleAsync(RemoveLoyaltyRedemptionCommand command)
    {
        var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId);
        if (invoice is null || invoice.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.InvoiceNotFound, nameof(command.InvoiceId), "Invoice not found.");

        CatEcommerce.Domain.ValueObjects.LoyaltyRedemption? removed;
        try
        {
            removed = invoice.RemoveLoyaltyRedemption(command.ToBillingActor());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (removed is not null)
            await LoyaltyPosting.ReverseRedemptionAsync(loyalty, invoice, removed, timeProvider.GetUtcNow(), command.ToBillingActor());

        return Success(invoice.Id);
    }
}

/// <summary>
/// C13 (US-BIL-08) -- pays an invoice from the customer free credit: payments that are not earmarked (or whose earmark was released), <b>oldest
/// acceptance first</b> (U-I6), each draw an allocation on the payment and a payment on the invoice, all in one save. An earmarked deposit is
/// never touched. <c>Amount</c> null means as much as the credit and the outstanding allow.
/// </summary>
public sealed class ApplyCustomerCreditCommandHandler(
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IAggregateRootRepository<PaymentAggregate> paymentRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IBillingFinder finder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ApplyCustomerCreditCommand>
{
    public async Task<CommandResponse> HandleAsync(ApplyCustomerCreditCommand command)
    {
        var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId);
        if (invoice is null || invoice.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.InvoiceNotFound, nameof(command.InvoiceId), "Invoice not found.");

        if (!await PaymentAllocating.BelongsToCustomerAsync(customerRepository, invoice.CustomerId, command.CustomerId))
            return Violation(BusinessViolationCodes.AllocationInvoiceCustomerMismatch, nameof(command.InvoiceId), "Credit can only be applied to the customer own invoices.");

        if (!InvoiceActionRules.CanApplyPayment(invoice.Status))
            return Violation(BusinessViolationCodes.InvoiceNotIssued, nameof(command.InvoiceId), "Credit can only be applied to an invoice that is issued and not yet settled.");

        var paymentIds = await finder.FindFreeCreditPaymentIdsAsync(command.CustomerId);
        if (paymentIds.Count == 0)
            return Violation(BusinessViolationCodes.CustomerCreditUnavailable, nameof(command.CustomerId), "This customer has no credit to apply.");

        var wanted = Math.Min(command.Amount ?? invoice.Outstanding, invoice.Outstanding);
        if (command.Amount is { } asked && asked > invoice.Outstanding)
            return Violation(BusinessViolationCodes.AllocationExceedsOutstanding, nameof(command.Amount),
                BillingMessages.AllocationExceedsOutstanding(invoice.Outstanding, invoice.CurrencyCode, invoice.Number ?? string.Empty));

        var actor = command.ToBillingActor();
        var now = timeProvider.GetUtcNow();
        var applied = new List<AppliedAllocation>();
        var participants = new List<IPostingParticipant>();
        var remaining = wanted;
        var freeLeft = 0m;

        try
        {
            foreach (var paymentId in paymentIds)
            {
                var payment = await paymentRepository.GetByIdAsync(paymentId);
                if (payment is null) continue;

                var draw = Math.Min(payment.Unallocated, remaining);
                if (draw <= 0m)
                {
                    freeLeft += payment.Unallocated;
                    continue;
                }

                payment.Allocate(new PaymentAllocateDto(invoice.Id, draw, true, now, actor));
                invoice.ApplyPayment(draw, actor);
                participants.Add(BillingPosting.Of(paymentRepository, payment));
                applied.Add(new AppliedAllocation(invoice.Id, invoice.Number ?? string.Empty, draw, invoice.Outstanding));
                remaining -= draw;
                freeLeft += payment.Unallocated;
            }
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (applied.Count == 0)
            return Violation(BusinessViolationCodes.CustomerCreditUnavailable, nameof(command.CustomerId), "There is nothing to apply.");

        await BillingPosting.CommitAsync(invoiceRepository, invoice, [.. participants]);
        return Success(new AppliedCustomerCreditResult(applied, freeLeft, invoice.Outstanding));
    }
}
