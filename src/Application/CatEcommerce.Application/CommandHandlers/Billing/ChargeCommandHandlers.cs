using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
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

/// <summary>HO-BIL-001 §11.4 C2 -- a desk charge. The customer must exist and be active; a service's price and tax
/// class are read <b>at now</b> (HO-ADM-001 ADR-049); the tax class must exist (its <i>rate</i> is resolved at
/// issue, not here -- ADR-181).</summary>
public sealed class RecordManualChargeCommandHandler(
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IAggregateRootRepository<ChargeAggregate> chargeRepository,
    IBillingPricingInputs pricingInputs,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordManualChargeCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordManualChargeCommand command)
    {
        var customer = await customerRepository.GetByIdAsync(command.CustomerId);
        if (customer is null || customer.Status != CustomerAccountStatus.Active)
            return Violation(BusinessViolationCodes.CustomerNotFound, nameof(command.CustomerId), "Customer not found.");

        var now = timeProvider.GetUtcNow();
        var currency = businessSettings.GetString(SettingKeys.CommerceCurrencyCode);

        string description;
        decimal unitPrice;
        Guid taxClassId;
        ItemKind itemKind;

        if (command.ServiceId is { } serviceId)
        {
            var service = await pricingInputs.GetServicePriceAsync(serviceId, now);
            if (service is null || !service.IsActive)
                return Violation(BusinessViolationCodes.ServiceNotFound, nameof(command.ServiceId), "Service not found.");
            if (service.Price is null)
                return Violation(BusinessViolationCodes.ServiceNotPriced, nameof(command.ServiceId), "This service has no price in force yet.");

            description = service.Name;
            unitPrice = service.Price.Amount;
            currency = service.Price.CurrencyCode;
            taxClassId = service.TaxClassId;
            itemKind = ItemKind.Service;
        }
        else
        {
            description = command.Description!;
            unitPrice = command.UnitPrice!.Value;
            taxClassId = command.TaxClassId!.Value;
            itemKind = ItemKind.Fee;
        }

        var rates = await pricingInputs.GetTaxRatesAsync([taxClassId], now);
        if (!rates.ContainsKey(taxClassId))
            return Violation(BusinessViolationCodes.TaxClassNotFound, nameof(command.TaxClassId), "Tax class not found.");

        var businessLine = string.IsNullOrWhiteSpace(command.BusinessLine) ? BusinessLine.Clinic : BillingMapper.ParseEnum<BusinessLine>(command.BusinessLine);

        var request = new ChargeRequestDto(
            command.CustomerId, command.BranchId, command.CatId, businessLine, itemKind, ChargeSourceKind.Manual, command.ChargeId,
            ChargeRules.DefaultSourceLineRef, null, null, description, command.Quantity,
            new Money(unitPrice, currency), new Money(command.LineDiscount, currency), taxClassId, command.ServiceDate);

        var charge = new ChargeAggregate(command.ChargeId);
        try
        {
            charge.Record(request, new ChargeRecordContext(businessSettings.GetString(SettingKeys.CommerceCurrencyCode), now, command.ToBillingActor()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await chargeRepository.SaveAsync(charge, EventPublishOption.InMemoryPublishOnly);
        return Success(charge.Id);
    }
}

/// <summary>HO-BIL-001 §11.4 C3 -- void an unbilled charge. A charge of another branch is "not found" (HO-ADM-001
/// §8.2); one already on an invoice is refused (corrected by credit note instead).</summary>
public sealed class VoidChargeCommandHandler(
    IBillingFinder finder,
    IAggregateRootRepository<ChargeAggregate> chargeRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<VoidChargeCommand>
{
    public async Task<CommandResponse> HandleAsync(VoidChargeCommand command)
    {
        var charge = await chargeRepository.GetByIdAsync(command.ChargeId);
        if (charge is null || charge.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.ChargeNotFound, nameof(command.ChargeId), "Charge not found.");

        if ((await finder.GetBilledChargeIdsAsync([charge.Id])).Contains(charge.Id))
            return Violation(BusinessViolationCodes.ChargeAlreadyInvoiced, nameof(command.ChargeId), BillingMessages.ChargeAlreadyInvoiced);


        try
        {
            charge.Void(command.Reason, timeProvider.GetUtcNow(), command.ToBillingActor());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await chargeRepository.UpdateAsync(charge, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>One item a capture could not record, and why -- reported, never guessed (§8.5).</summary>
public sealed record CaptureSkippedItem(string Reason);

/// <summary>The result of C4: <c>{ captured, skipped: [{ reason }] }</c>.</summary>
public sealed record CaptureSourceChargesResult(int Captured, IReadOnlyList<CaptureSkippedItem> Skipped);

/// <summary>
/// HO-BIL-001 §11.4 C4 / §8.5 (ADR-178) -- the pull path. Resolves the item source for the kind, records every
/// item not yet recorded (one save each, idempotent by source key), skips and reports an item whose tax class is
/// missing. A source that exists but is not billable yet succeeds with <c>captured: 0</c> and the reason. What the source itself could not turn into an
/// item (<see cref="BillableSourceResult.Skipped"/>) is reported beside the handler's own skips.
/// </summary>
public sealed class CaptureSourceChargesCommandHandler(
    IBillableItemSourceResolver sourceResolver,
    IAggregateRootRepository<ChargeAggregate> chargeRepository,
    IBillingPricingInputs pricingInputs,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CaptureSourceChargesCommand>
{
    public async Task<CommandResponse> HandleAsync(CaptureSourceChargesCommand command)
    {
        var kind = BillingMapper.ParseEnum<ChargeSourceKind>(command.SourceKind);
        var source = sourceResolver.Resolve(kind);
        if (source is null)
            return Violation(BusinessViolationCodes.SourceKindNotCapturable, nameof(command.SourceKind), "Charges of this kind are not captured from a source.");

        var result = await source.GetItemsAsync(command.SourceId);
        if (!result.Exists || (command.BranchId is { } scopedBranch && result.BranchId is { } sourceBranch && sourceBranch != scopedBranch)
            || (command.BranchId is { } branchId && result.Items.Any(i => i.BranchId != branchId)))
            return Violation(BusinessViolationCodes.SourceNotFound, nameof(command.SourceId), "Source not found.");

        if (!result.IsBillable)
            return Success(new CaptureSourceChargesResult(0, [new CaptureSkippedItem(result.Reason ?? "NotBillable")]));

        var now = timeProvider.GetUtcNow();
        var currency = businessSettings.GetString(SettingKeys.CommerceCurrencyCode);
        var taxClassIds = result.Items.Where(i => i.TaxClassId is not null).Select(i => i.TaxClassId!.Value).Distinct().ToList();
        var knownClasses = await pricingInputs.GetTaxRatesAsync(taxClassIds, now);

        var captured = 0;
        var skipped = new List<CaptureSkippedItem>(result.Skipped.Select(reason => new CaptureSkippedItem(reason)));
        foreach (var item in result.Items)
        {
            var lineRef = string.IsNullOrWhiteSpace(item.SourceLineRef) ? ChargeRules.DefaultSourceLineRef : item.SourceLineRef;
            if (await chargeRepository.ExistAsync(c => c.SourceKind == kind && c.SourceId == command.SourceId && c.SourceLineRef == lineRef))
                continue; // already recorded: a re-run records only what is missing

            if (item.TaxClassId is not { } taxClassId || !knownClasses.ContainsKey(taxClassId))
            {
                skipped.Add(new CaptureSkippedItem(BusinessViolationCodes.TaxClassNotFound));
                continue;
            }

            var request = new ChargeRequestDto(
                item.CustomerId, item.BranchId, item.CatId, ChargeRules.DefaultBusinessLine(kind), item.ItemKind, kind, command.SourceId, lineRef,
                item.BillingGroupKind, item.BillingGroupId, item.Description, item.Quantity, item.UnitPrice, item.LineDiscount, taxClassId, item.ServiceDate);

            var charge = new ChargeAggregate(Guid.NewGuid());
            try
            {
                charge.Record(request, new ChargeRecordContext(currency, now, command.ToBillingActor()));
            }
            catch (DomainRuleViolationException ex)
            {
                skipped.Add(new CaptureSkippedItem(ex.ErrorCode));
                continue;
            }

            await chargeRepository.SaveAsync(charge, EventPublishOption.InMemoryPublishOnly);
            captured++;
        }

        return Success(new CaptureSourceChargesResult(captured, skipped));
    }
}
