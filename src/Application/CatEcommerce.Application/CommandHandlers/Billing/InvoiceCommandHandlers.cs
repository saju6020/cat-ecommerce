using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Billing.BillingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Billing;

/// <summary>
/// The pre-checks C5, C6 and C9 share (HO-BIL-001 §11.4): every named charge exists at this branch, is active,
/// belongs to the customer, is in the invoice currency, and is not already billed. The filtered unique index
/// <c>ix_invoice_line_charge</c> is the race guard behind the "not already billed" check.
/// </summary>
public static class ChargeSelection
{
    public sealed record Result(IReadOnlyList<ChargeAggregate> Charges, CommandResponse? Violation);

    public static async Task<Result> LoadAndValidateAsync(
        IAggregateRootRepository<ChargeAggregate> chargeRepository, IBillingFinder finder, IBusinessSettings settings,
        Guid customerId, Guid? branchId, IReadOnlyList<Guid> chargeIds, IReadOnlySet<Guid>? alreadyOnThisInvoice = null)
    {
        var distinct = chargeIds.Distinct().ToList();
        if (distinct.Count == 0)
            return new Result([], Violation(BusinessViolationCodes.ChargeNotFound, "ChargeIds", "Choose at least one charge."));

        if (distinct.Count > BillingRules.MaxInvoiceLines)
            return new Result([], Violation(BusinessViolationCodes.InvoiceTooManyLines, "ChargeIds", $"An invoice can have at most {BillingRules.MaxInvoiceLines} lines."));

        var charges = new List<ChargeAggregate>();
        foreach (var id in distinct)
        {
            var charge = await chargeRepository.GetByIdAsync(id);
            if (charge is null || (branchId is { } branch && charge.BranchId != branch))
                return new Result([], Violation(BusinessViolationCodes.ChargeNotFound, "ChargeIds", "A charge was not found."));

            if (charge.Status != ChargeStatus.Active)
                return new Result([], Violation(BusinessViolationCodes.ChargeNotActive, "ChargeIds", "A voided charge cannot be invoiced."));

            charges.Add(charge);
        }

        if (charges.Any(c => c.CustomerId != customerId) || charges.Select(c => c.BranchId).Distinct().Count() > 1)
            return new Result([], Violation(BusinessViolationCodes.ChargesMixedCustomerOrBranch, "ChargeIds", "All charges of an invoice must belong to one customer and one branch."));

        var currency = settings.GetString(SettingKeys.CommerceCurrencyCode);
        if (charges.Any(c => c.CurrencyCode != currency))
            return new Result([], Violation(BusinessViolationCodes.CurrencyMismatch, "ChargeIds", $"An invoice must be in {currency}."));

        var billed = await finder.GetBilledChargeIdsAsync(distinct);
        if (billed.Any(id => alreadyOnThisInvoice is null || !alreadyOnThisInvoice.Contains(id)))
            return new Result([], Violation(BusinessViolationCodes.ChargeAlreadyInvoiced, "ChargeIds", BillingMessages.ChargeAlreadyInvoiced));

        return new Result(charges, null);
    }
}

/// <summary>HO-BIL-001 §11.4 C5 -- a draft from unbilled charges. The customer must exist and be active.</summary>
public sealed class CreateDraftInvoiceCommandHandler(
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IAggregateRootRepository<ChargeAggregate> chargeRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IBillingFinder finder,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CreateDraftInvoiceCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateDraftInvoiceCommand command)
    {
        var customer = await customerRepository.GetByIdAsync(command.CustomerId);
        if (customer is null || customer.Status != CustomerAccountStatus.Active)
            return Violation(BusinessViolationCodes.CustomerNotFound, nameof(command.CustomerId), "Customer not found.");

        var selection = await ChargeSelection.LoadAndValidateAsync(
            chargeRepository, finder, businessSettings, command.CustomerId, command.BranchId, command.ChargeIds);
        if (selection.Violation is not null) return selection.Violation;

        var invoice = new InvoiceAggregate(command.InvoiceId);
        try
        {
            invoice.CreateDraft(new DraftInvoiceDto(
                command.BranchId, command.CustomerId, businessSettings.GetString(SettingKeys.CommerceCurrencyCode),
                selection.Charges.Select(c => c.ToDraftLine()).ToList(), command.Notes, null, null, timeProvider.GetUtcNow(), command.ToBillingActor()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await invoiceRepository.SaveAsync(invoice, EventPublishOption.InMemoryPublishOnly);
        return Success(invoice.Id);
    }
}

/// <summary>HO-BIL-001 §11.4 C6 -- replace a draft charges (<c>ExpectedVersion</c>, else <c>409</c>).</summary>
public sealed class SetDraftInvoiceChargesCommandHandler(
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IAggregateRootRepository<ChargeAggregate> chargeRepository,
    IBillingFinder finder,
    IBusinessSettings businessSettings,
    LoyaltyDependencies loyalty,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SetDraftInvoiceChargesCommand>
{
    public async Task<CommandResponse> HandleAsync(SetDraftInvoiceChargesCommand command)
    {
        var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId);
        if (invoice is null || invoice.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.InvoiceNotFound, nameof(command.InvoiceId), "Invoice not found.");

        if (invoice.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleVersion, nameof(command.ExpectedVersion), "This invoice has changed since you loaded it. Reload and try again.");

        if (invoice.Status != InvoiceStatus.Draft)
            return Violation(BusinessViolationCodes.InvoiceNotDraft, nameof(command.InvoiceId), BillingMessages.InvoiceNotDraft);

        var own = invoice.Lines.Where(l => l.ChargeId is not null).Select(l => l.ChargeId!.Value).ToHashSet();
        var selection = await ChargeSelection.LoadAndValidateAsync(
            chargeRepository, finder, businessSettings, invoice.CustomerId, invoice.BranchId, command.ChargeIds, own);
        if (selection.Violation is not null) return selection.Violation;

        LoyaltyRedemption? cleared;
        try
        {
            cleared = invoice.SetDraftLines(selection.Charges.Select(c => c.ToDraftLine()).ToList(), command.ToBillingActor());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }
        if (cleared is not null)
            await LoyaltyPosting.ReverseRedemptionAsync(loyalty, invoice, cleared, timeProvider.GetUtcNow(), command.ToBillingActor());
        else
            await invoiceRepository.UpdateAsync(invoice, EventPublishOption.InMemoryPublishOnly);
        return Success(invoice.Id);
    }
}

/// <summary>HO-BIL-001 §11.4 C6 -- discard a draft: its lines leave the unique index's scope, so its charges are
/// unbilled again and a new draft can take them (I-C6).</summary>
public sealed class DiscardDraftInvoiceCommandHandler(IAggregateRootRepository<InvoiceAggregate> invoiceRepository, LoyaltyDependencies loyalty, TimeProvider timeProvider)
    : ICommandHandlerAsync<DiscardDraftInvoiceCommand>
{
    public async Task<CommandResponse> HandleAsync(DiscardDraftInvoiceCommand command)
    {
        var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId);
        if (invoice is null || invoice.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.InvoiceNotFound, nameof(command.InvoiceId), "Invoice not found.");


        LoyaltyRedemption? redemption;
        try
        {
            redemption = invoice.Discard(command.ToBillingActor());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (redemption is not null)
            await LoyaltyPosting.ReverseRedemptionAsync(loyalty, invoice, redemption, timeProvider.GetUtcNow(), command.ToBillingActor());
        else
            await invoiceRepository.UpdateAsync(invoice, EventPublishOption.InMemoryPublishOnly);

        return Success();
    }
}

/// <summary>HO-BIL-001 §11.4 C8 -- issue a draft. An <c>Idempotency-Key</c> already used by this caller returns the original
/// result (<c>200</c>); the same key on a different invoice is <c>409 IdempotencyKeyConflict</c> (ADR-187).</summary>
public sealed class IssueInvoiceCommandHandler(
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAggregateRootRepository<BillingProfileAggregate> profileRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IAggregateRootRepository<CustomerAddressBookAggregate> addressBookRepository,
    IBillingPricingInputs pricingInputs,
    IDocumentNumbering numbering,
    IBusinessSettings businessSettings,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    IBillingFinder finder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<IssueInvoiceCommand>
{
    public async Task<CommandResponse> HandleAsync(IssueInvoiceCommand command)
    {
        if (command.IdempotencyKey is { } key)
        {
            var prior = await finder.FindInvoiceByIdempotencyKeyAsync(command.UserContext!.UserId, key);
            if (prior is not null)
                return prior.InvoiceId == command.InvoiceId
                    ? Success(new IssuedInvoiceResult(prior.InvoiceId, prior.Number, Replayed: true))
                    : Violation(BusinessViolationCodes.IdempotencyKeyConflict, nameof(command.IdempotencyKey), BillingMessages.IdempotencyKeyConflict);
        }

        var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId);
        if (invoice is null || invoice.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.InvoiceNotFound, nameof(command.InvoiceId), "Invoice not found.");

        if (invoice.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleVersion, nameof(command.ExpectedVersion), "This invoice has changed since you loaded it. Reload and try again.");

        if (invoice.Status != InvoiceStatus.Draft)
            return Violation(BusinessViolationCodes.InvoiceNotDraft, nameof(command.InvoiceId), BillingMessages.InvoiceNotDraft);

        var dependencies = new InvoiceIssueDependencies(
            invoiceRepository, branchRepository, profileRepository, customerRepository, addressBookRepository,
            pricingInputs, numbering, businessSettings, outbox, links, timeProvider);

        return await InvoiceIssuing.IssueAsync(dependencies, invoice, isNew: false, command, command.IdempotencyKey);
    }
}

/// <summary>
/// HO-BIL-001 §11.4 C9 -- the published contract of §8.13: resolve the selector to unbilled active charges of the
/// customer, create the draft and issue it <b>in the same save</b> (no draft is ever visible). A repeat of the same
/// <c>Idempotency-Key</c> returns the original invoice.
/// </summary>
public sealed class IssueInvoiceFromChargesCommandHandler(
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IAggregateRootRepository<ChargeAggregate> chargeRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAggregateRootRepository<BillingProfileAggregate> profileRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IAggregateRootRepository<CustomerAddressBookAggregate> addressBookRepository,
    IBillingPricingInputs pricingInputs,
    IDocumentNumbering numbering,
    IBusinessSettings businessSettings,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    IBillingFinder finder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<IssueInvoiceFromChargesCommand>
{
    public async Task<CommandResponse> HandleAsync(IssueInvoiceFromChargesCommand command)
    {
        if (command.Selector.FormCount != 1)
            return Violation(BusinessViolationCodes.ChargeNotFound, nameof(command.Selector), "Give exactly one of chargeIds, source or billingGroup.");

        if (command.IdempotencyKey is { } key)
        {
            var prior = await finder.FindInvoiceByIdempotencyKeyAsync(command.UserContext!.UserId, key);
            if (prior is not null)
                return prior.CustomerId == command.CustomerId
                    ? Success(new IssuedInvoiceResult(prior.InvoiceId, prior.Number, Replayed: true))
                    : Violation(BusinessViolationCodes.IdempotencyKeyConflict, nameof(command.IdempotencyKey), BillingMessages.IdempotencyKeyConflict);
        }

        var customer = await customerRepository.GetByIdAsync(command.CustomerId);
        if (customer is null || customer.Status != CustomerAccountStatus.Active)
            return Violation(BusinessViolationCodes.CustomerNotFound, nameof(command.CustomerId), "Customer not found.");

        IReadOnlyList<Guid> chargeIds;
        if (command.Selector.ChargeIds is { } named)
        {
            chargeIds = named;
        }
        else
        {
            var criteria = command.Selector.Source is { } source
                ? new UnbilledChargeCriteria(command.CustomerId, command.BranchId, BillingMapper.ParseEnum<ChargeSourceKind>(source.Kind), source.Id, null, null, null)
                : new UnbilledChargeCriteria(command.CustomerId, command.BranchId, null, null,
                    BillingMapper.ParseEnum<BillingGroupKind>(command.Selector.BillingGroup!.Kind), command.Selector.BillingGroup.Id, command.Selector.BillingGroup.ServiceDateTo);

            chargeIds = await finder.FindUnbilledChargeIdsAsync(criteria);
        }

        var selection = await ChargeSelection.LoadAndValidateAsync(
            chargeRepository, finder, businessSettings, command.CustomerId, command.BranchId, chargeIds);
        if (selection.Violation is not null) return selection.Violation;

        var branchId = selection.Charges[0].BranchId;
        var invoice = new InvoiceAggregate(command.InvoiceId);
        try
        {
            invoice.CreateDraft(new DraftInvoiceDto(
                branchId, command.CustomerId, businessSettings.GetString(SettingKeys.CommerceCurrencyCode),
                selection.Charges.Select(c => c.ToDraftLine()).ToList(), null, null, null, timeProvider.GetUtcNow(), command.ToBillingActor()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        var dependencies = new InvoiceIssueDependencies(
            invoiceRepository, branchRepository, profileRepository, customerRepository, addressBookRepository,
            pricingInputs, numbering, businessSettings, outbox, links, timeProvider);

        return await InvoiceIssuing.IssueAsync(dependencies, invoice, isNew: true, command, command.IdempotencyKey, command.PinnedTaxClassId, command.PinnedTaxRateAsOf);
    }
}
