using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Prescription.PharmacyHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Prescription;

/// <summary>
/// HO-PHM-001 §8.7 / §11.4 C9 (US-PHM-05, US-PHM-04 AC2, US-PHM-06 AC1; ADR-143, ADR-147, ADR-154) -- dispense one supply. Everything is decided BEFORE anything is
/// written, then committed in ONE <c>SaveChangesAsync</c> -- the dispensing record, the prescription's supply count and any repeat request it fulfils, HO-IMP-001's ledger
/// entries (enlisted through <see cref="IStockLedger.DeductAsync"/>, naming the EXACT batches), the controlled-drug register entries (enlisted through
/// <see cref="IControlledDrugRegister"/>) and the "repeat ready" message. A refusal anywhere returns a violation and saves nothing; a race (another dispenser, the last
/// unit) fails at commit and rolls the lot back (the prescription's <c>xmin</c>, <c>ix_dispensing_supply</c>, <c>ck_stock_position_balance</c>).
///
/// Order: the prescription may be supplied now (validity, supplies left, not cancelled) -> the cat is alive -> Inventory's dispensary batches go through the pure
/// <see cref="DispensingBatchPlanner"/> (<b>an expired batch is refused; there is no override</b>, BR-3) -> a controlled medicine's witness is verified -> only then are
/// movements and entries enlisted.
/// </summary>
public sealed class DispensePrescriptionCommandHandler(
    IAggregateRootRepository<PrescriptionAggregate> prescriptionRepository,
    IAggregateRootRepository<DispensingAggregate> dispensingRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IPharmacyClock clock,
    IStockLedger ledger,
    IControlledDrugRegister register,
    IStaffWitnessVerifier witnessVerifier,
    ISkuPriceReader prices,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<DispensePrescriptionCommand>
{
    public async Task<CommandResponse> HandleAsync(DispensePrescriptionCommand command)
    {
        var ct = CancellationToken.None;
        var user = command.UserContext!;
        var now = timeProvider.GetUtcNow();

        var prescription = await prescriptionRepository.GetByIdAsync(command.PrescriptionId);
        if (prescription is null)
            return Violation(BusinessViolationCodes.PrescriptionNotFound, nameof(command.PrescriptionId), "Prescription not found.");

        var cat = await catRepository.GetByIdAsync(prescription.CatId);
        if (cat is null || cat.LifeStatus != CatLifeStatus.Alive)
            return Violation(BusinessViolationCodes.CatNotTreatable, nameof(command.PrescriptionId), "This cat can no longer be treated, so nothing can be dispensed.");

        // 1. The prescription may be supplied TODAY (branch-local): not cancelled, supplies left, inside ValidUntil -- whether or not the expiry sweep has run.
        var today = await clock.LocalTodayAsync(command.BranchId, ct);
        try { prescription.EnsureSupplyAllowed(today); }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        if (command.RepeatRequestId is { } requestId && !prescription.RepeatRequests.Any(r => r.Id == requestId && r.IsOpen))
            return Violation(BusinessViolationCodes.RepeatRequestNotFound, nameof(command.RepeatRequestId), "That repeat request is not open on this prescription.");

        var medicine = prescription.MedicineSnapshot;
        var linkedSkus = medicine.LinkedSkuIds;
        if (linkedSkus.Length == 0)
            return Violation(BusinessViolationCodes.MedicineNotStocked, nameof(command.PrescriptionId), $"{medicine.Name} has no stocked SKU, so it cannot be dispensed from the clinic.");

        // 2. Inventory's batches at this branch's dispensary, through the pure planner. Expired stock is never suggested and never accepted.
        var held = await ledger.GetDispensaryBatchesAsync(command.BranchId, linkedSkus, ct);
        if (!held.IsSuccess)
            return Violation(held.ViolationCode!, held.PropertyName ?? nameof(command.Lines), held.Message ?? "The dispensary stock could not be read.");

        var offers = BatchOfferFactory.FromCandidates(linkedSkus, held.Value!);
        var plan = DispensingBatchPlanner.Plan(prescription.Quantity, offers, today);

        if (plan.OnlyExpiredStockAvailable)
        {
            var expired = plan.ExpiredExample!;
            return Violation(BusinessViolationCodes.OnlyExpiredStockAvailable, nameof(command.Lines), PharmacyMessages.OnlyExpiredStockAvailable(medicine.Name, expired.LotNumber, expired.ExpiresOn!.Value));
        }

        var requested = command.Lines.Select(l => new DispenseLineInput(l.SkuId, l.BatchId, l.Quantity)).ToList();
        var violation = DispensingBatchPlanner.Validate(requested, prescription.Quantity, offers, linkedSkus, today, command.FefoOverrideReason).FirstOrDefault();
        if (violation is not null) return Violation(violation.Code, violation.PropertyName, violation.Message);

        if (plan.TotalDispensable < prescription.Quantity)
            return Violation(BusinessViolationCodes.InsufficientDispensaryStock, nameof(command.Lines), PharmacyMessages.InsufficientDispensaryStock(plan.TotalDispensable));

        // 3. A controlled medicine: the witness, verified BEFORE any write.
        Guid? witnessId = null;
        if (medicine.IsControlled)
        {
            if (string.IsNullOrWhiteSpace(command.WitnessIdentifier) || string.IsNullOrWhiteSpace(command.WitnessPassword))
                return Violation(BusinessViolationCodes.WitnessRequired, nameof(command.WitnessIdentifier), PharmacyMessages.WitnessRequired);

            var witness = await witnessVerifier.VerifyAsync(command.WitnessIdentifier, command.WitnessPassword, user.UserId, ControlledDrugSourceType.Dispensing, ct);
            if (!witness.IsVerified)
                return Violation(BusinessViolationCodes.WitnessNotVerified, nameof(command.WitnessIdentifier), PharmacyMessages.WitnessNotVerified);
            witnessId = witness.WitnessStaffId;
        }

        // 4. Names for the label and the branch's details -- read once, frozen into the label.
        var owner = await customerRepository.GetByIdAsync(prescription.CustomerId);
        var prescriber = await staffRepository.GetByIdAsync(prescription.PrescriberId);
        var branch = await branchRepository.GetByIdAsync(command.BranchId);
        var supplyNumber = prescription.SuppliesDispensed + 1;
        var reference = $"RX {prescription.PrescriptionNumber} #{supplyNumber}";

        // 5. Movements, ENLISTED (not saved) -- one demand per SKU naming the exact batches, so FEFO is compared per SKU against what is really in the dispensary.
        var offersByBatch = offers.ToDictionary(o => o.BatchId);
        var lines = new List<DispensingLineInput>();
        foreach (var group in requested.GroupBy(l => l.SkuId))
        {
            var referenceLineId = Guid.NewGuid();
            var explicitLines = group.Select(l => (l.BatchId, offersByBatch[l.BatchId].LocationId, l.Quantity)).ToList();
            var deducted = await ledger.DeductAsync(
                new StockDemand(command.BranchId, group.Key, group.Sum(l => l.Quantity), MovementKind.Dispense, null, explicitLines, command.FefoOverrideReason?.Trim()),
                new StockReference(StockReferenceKind.Prescription, command.DispensingId, referenceLineId, reference),
                new StockParty(prescription.CustomerId, prescription.CatId), ct);

            if (!deducted.IsSuccess)
                return Violation(deducted.ViolationCode!, deducted.PropertyName ?? nameof(command.Lines), deducted.Message ?? "The stock could not be taken.");

            foreach (var movement in deducted.Value!)
            {
                var lineId = Guid.NewGuid();
                Guid? registerEntryId = null;
                if (medicine.IsControlled)
                {
                    registerEntryId = EnlistRegisterEntry(
                        register, command, prescription, medicine, group.Key, movement, witnessId!.Value, now, user.UserId);
                }

                var price = await prices.PriceAtAsync(group.Key, now, ct);
                var snapshot = price is null ? null : new PriceSnapshot(price.Amount, decimal.Round(price.Amount * movement.Quantity, 2, MidpointRounding.AwayFromZero), price.CurrencyCode);
                lines.Add(new DispensingLineInput(lineId, group.Key, movement.BatchId, movement.LotNumber, movement.ExpiresOn, movement.Quantity, registerEntryId, snapshot));
            }
        }

        // 6. The label, frozen, and the aggregates.
        var label = MedicineLabelComposer.Compose(new MedicineLabelInput(
            cat.Name, owner?.FullName ?? "", medicine.Name, medicine.StrengthText, medicine.IsControlled, prescription.Dose, prescription.Route, prescription.Frequency,
            prescription.DurationDays, prescription.Instructions, medicine.DispensingUnit, prescription.Quantity, today, lines.Select(l => l.ExpiresOn).ToList(),
            prescription.PrescriptionNumber, prescriber?.FullName ?? "", branch?.Name ?? "", branch?.PhoneNumber ?? "", branch?.AddressLine));

        try { prescription.RecordSupply(command.DispensingId, today, command.RepeatRequestId, user.UserId, now); }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        var dispensing = new DispensingAggregate(command.DispensingId);
        try
        {
            dispensing.Create(new DispensingCreateDto(
                prescription.Id, prescription.PrescriptionNumber, command.BranchId, prescription.CatId, prescription.CustomerId, prescription.VisitId, prescription.SuppliesDispensed,
                command.RepeatRequestId is not null || prescription.SuppliesDispensed > 1, command.RepeatRequestId, user.UserId, now, today, witnessId, command.FefoOverrideReason?.Trim(),
                lines, label, command.ToPharmacyActor()));
        }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        if (command.RepeatRequestId is not null)
        {
            PharmacyNotifications.EnqueueRepeatReady(
                outbox, links, cat.OwnerId, prescription.Id, cat.Name, medicine.Name, branch?.Name ?? "the clinic", command.BranchId, command.CorrelationId, user);
        }

        // 7. ONE save: the dispensing, the tracked prescription, the ledger entries, the register entries and the notification commit together (F23).
        await dispensingRepository.SaveAsync(dispensing, EventPublishOption.InMemoryPublishOnly);

        return Success(new DispensedResult(dispensing.Id, dispensing.SupplyNumber, dispensing.Label, lines.Where(l => l.Price is null).Select(l => l.LineId).ToList()));
    }

    private static Guid EnlistRegisterEntry(
        IControlledDrugRegister register, DispensePrescriptionCommand command, PrescriptionAggregate prescription, MedicineSnapshot medicine, Guid skuId, StockMovementLine movement,
        Guid witnessId, DateTimeOffset now, Guid dispenserId) =>
        register.Enlist(new ControlledDrugRegisterEntryDto(
            command.BranchId, prescription.MedicineId, medicine.Name, medicine.StrengthText, medicine.Form, medicine.ControlledSchedule, medicine.DispensingUnit, skuId, movement.BatchId,
            movement.LotNumber, movement.ExpiresOn, ControlledDrugEntryKind.Dispensed, -movement.Quantity,
            DoseFormatter.Format(prescription.Dose, prescription.Route, prescription.Frequency, prescription.DurationDays), null, prescription.CatId, prescription.CustomerId,
            dispenserId, witnessId, ControlledDrugSourceType.Dispensing, command.DispensingId, null, null, null, now, null));
}

/// <summary>HO-PHM-001 §11.4 C10 (ADR-147) -- a label print is counted and audited (a reprint of a controlled-drug label is worth knowing about). The label printed is always the
/// frozen snapshot.</summary>
public sealed class RecordLabelPrintCommandHandler(
    IAggregateRootRepository<DispensingAggregate> dispensingRepository, IAggregateRootRepository<PrescriptionAggregate> prescriptionRepository)
    : ICommandHandlerAsync<RecordLabelPrintCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordLabelPrintCommand command)
    {
        var dispensing = await dispensingRepository.GetByIdAsync(command.DispensingId);
        if (dispensing is null || dispensing.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.DispensingNotFound, nameof(command.DispensingId), "Dispensing not found.");

        var prescription = await prescriptionRepository.GetByIdAsync(dispensing.PrescriptionId);
        dispensing.RecordLabelPrint(prescription?.MedicineSnapshot.IsControlled ?? false, command.ToPharmacyActor());

        await dispensingRepository.UpdateAsync(dispensing, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// HO-PHM-001 §8.7 / §11.4 C11 -- reverse the latest supply within <c>pharmacy.dispensingReversalHours</c> (Vet, Administrator; ADR-143 -- one save). It enlists a
/// <c>Return</c> movement per line through <see cref="IStockLedger.ReceiveReturnAsync"/> (Inventory puts a return into the branch's Quarantine location -- what happens next is
/// an Inventory judgement), a register <c>Returned</c> entry per controlled line linked to the original entry (with a verified witness), takes the supply back off the
/// prescription and marks the dispensing reversed.
/// </summary>
public sealed class ReverseDispensingCommandHandler(
    IAggregateRootRepository<DispensingAggregate> dispensingRepository,
    IAggregateRootRepository<PrescriptionAggregate> prescriptionRepository,
    IStockLedger ledger,
    IControlledDrugRegister register,
    IStaffWitnessVerifier witnessVerifier,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ReverseDispensingCommand>
{
    public async Task<CommandResponse> HandleAsync(ReverseDispensingCommand command)
    {
        var ct = CancellationToken.None;
        var user = command.UserContext!;
        var now = timeProvider.GetUtcNow();

        var dispensing = await dispensingRepository.GetByIdAsync(command.DispensingId);
        if (dispensing is null || dispensing.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.DispensingNotFound, nameof(command.DispensingId), "Dispensing not found.");

        var prescription = await prescriptionRepository.GetByIdAsync(dispensing.PrescriptionId);
        if (prescription is null)
            return Violation(BusinessViolationCodes.PrescriptionNotFound, nameof(command.DispensingId), "Prescription not found.");

        // Pre-checks that need no write: only the latest supply, reason, window.
        var windowHours = settings.GetInt(SettingKeys.PharmacyDispensingReversalHours, dispensing.BranchId);
        if (string.IsNullOrWhiteSpace(command.Reason))
            return Violation(BusinessViolationCodes.ReversalReasonRequired, nameof(command.Reason), "Give a reason for the reversal.");
        if (dispensing.Status != DispensingStatus.Dispensed || dispensing.SupplyNumber != prescription.SuppliesDispensed)
            return Violation(BusinessViolationCodes.DispensingNotReversible, nameof(command.DispensingId), "Only the latest supply of a prescription can be reversed.");
        if (!dispensing.IsWithinReversalWindow(now, windowHours))
            return Violation(BusinessViolationCodes.ReversalWindowPassed, nameof(command.DispensingId), "This supply was dispensed too long ago to reverse. Waste or return the medicine through Inventory instead.");

        var medicine = prescription.MedicineSnapshot;
        Guid? witnessId = null;
        if (medicine.IsControlled)
        {
            if (string.IsNullOrWhiteSpace(command.WitnessIdentifier) || string.IsNullOrWhiteSpace(command.WitnessPassword))
                return Violation(BusinessViolationCodes.WitnessRequired, nameof(command.WitnessIdentifier), PharmacyMessages.WitnessRequired);

            var witness = await witnessVerifier.VerifyAsync(command.WitnessIdentifier, command.WitnessPassword, user.UserId, ControlledDrugSourceType.DispensingReversal, ct);
            if (!witness.IsVerified)
                return Violation(BusinessViolationCodes.WitnessNotVerified, nameof(command.WitnessIdentifier), PharmacyMessages.WitnessNotVerified);
            witnessId = witness.WitnessStaffId;
        }

        // The supply before this one, for the prescription's "last dispensed" date.
        var previous = dispensing.SupplyNumber > 1
            ? await dispensingRepository.GetByFilterAsync(d => d.PrescriptionId == prescription.Id && d.SupplyNumber == dispensing.SupplyNumber - 1 && d.Status == DispensingStatus.Dispensed)
            : null;

        try
        {
            dispensing.Reverse(user.UserId, command.Reason, now, windowHours, command.ToPharmacyActor());
            prescription.ReverseSupply(dispensing.SupplyNumber, previous?.DispensedOnLocal);
        }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        foreach (var line in dispensing.Lines)
        {
            var returned = await ledger.ReceiveReturnAsync(
                new StockReturn(dispensing.BranchId, line.SkuId, line.BatchId, line.Quantity, "Dispensing reversed"),
                new StockReference(StockReferenceKind.Prescription, dispensing.Id, line.Id, $"RX {prescription.PrescriptionNumber} #{dispensing.SupplyNumber} reversed"),
                new StockParty(dispensing.CustomerId, dispensing.CatId), ct);

            if (!returned.IsSuccess)
                return Violation(returned.ViolationCode!, returned.PropertyName ?? nameof(command.DispensingId), returned.Message ?? "The stock could not be returned.");

            if (medicine.IsControlled)
            {
                register.Enlist(new ControlledDrugRegisterEntryDto(
                    dispensing.BranchId, prescription.MedicineId, medicine.Name, medicine.StrengthText, medicine.Form, medicine.ControlledSchedule, medicine.DispensingUnit, line.SkuId,
                    line.BatchId, line.LotNumber, line.ExpiresOn, ControlledDrugEntryKind.Returned, line.Quantity, null, null, dispensing.CatId, dispensing.CustomerId, user.UserId,
                    witnessId, ControlledDrugSourceType.DispensingReversal, dispensing.Id, null, null, line.RegisterEntryId, now, "Dispensing reversed"));
            }
        }

        await dispensingRepository.UpdateAsync(dispensing, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
