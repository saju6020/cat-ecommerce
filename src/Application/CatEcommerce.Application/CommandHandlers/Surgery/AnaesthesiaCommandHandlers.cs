using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Surgery.SurgeryHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Surgery;

/// <summary>What a drug entry needs from the outside world, all decided BEFORE anything is written (the order of §8.9): the medicine, the batch, the witness. A failure here is a returned
/// violation; nothing has been enlisted yet.</summary>
internal sealed record DrugPlan(
    FormularyMedicineSnapshot Medicine, Guid? SkuId, DispensaryBatch? Batch, Guid? WitnessId, decimal? Drawn, decimal? Wasted);

/// <summary>The steps C8 and C10 share. A static helper over the ports -- not a service layer (ADR-015).</summary>
internal static class AnaesthesiaDrugSupport
{
    public static bool IsOpenForLogging(SurgeryAggregate surgery) =>
        surgery.Status == SurgeryStatus.PreOp || surgery.Status == SurgeryStatus.InTheatre || surgery.Status == SurgeryStatus.Recovery;

    public static CommandResponse Closed() => Violation(BusinessViolationCodes.AnaesthesiaLogClosed, "SurgeryId", SurgeryMessages.LogClosed);

    public static AnaesthesiaEntryContext Context(SurgeryAggregate surgery, DateTimeOffset occurredOn, DateTimeOffset now, Platform.Infrastructure.Common.Security.UserContext user, Guid correlationId) =>
        new(surgery.Id, surgery.CatId, surgery.BranchId, surgery.AdmittedOn, occurredOn.ToUniversalTime(), now, user, correlationId);

    /// <summary>The medicine from the formulary (active), the batch from the clinic dispensary (so its SKU and location are known), and for a controlled drug the administering vet, the
    /// wastage and a VERIFIED witness (BR-7, ADR-164).</summary>
    public static async Task<(DrugPlan? Plan, CommandResponse? Refusal)> PlanAsync(
        SurgeryAggregate surgery, Guid medicineId, Guid administeredById, Guid? batchId, decimal? drawn, decimal? wasted, string? witnessIdentifier, string? witnessPassword,
        Guid callerId, IFormularyReader formulary, IStockLedger ledger, IAggregateRootRepository<StaffAccountAggregate> staffRepository, IStaffWitnessVerifier witnessVerifier)
    {
        var medicine = await formulary.GetAsync(medicineId, CancellationToken.None);
        if (medicine is null || !medicine.IsActive)
            return (null, Violation(BusinessViolationCodes.MedicineNotFound, nameof(medicineId), "That medicine is not in the formulary or is no longer active."));

        // 3. Stock -- a medicine with a stocked SKU names its batch and the quantity drawn.
        DispensaryBatch? batch = null;
        if (medicine.LinkedSkuIds.Count > 0)
        {
            if (batchId is null || drawn is null)
                return (null, Violation(BusinessViolationCodes.BatchRequired, nameof(batchId), $"{medicine.Name} is a stocked medicine: name the batch and the quantity drawn."));

            var held = await ledger.GetDispensaryBatchesAsync(surgery.BranchId, medicine.LinkedSkuIds.ToList(), CancellationToken.None);
            if (!held.IsSuccess)
                return (null, Violation(held.ViolationCode!, nameof(batchId), held.Message ?? "The clinic stock could not be read."));

            batch = held.Value!.FirstOrDefault(b => b.BatchId == batchId);
            if (batch is null)
                return (null, Violation(BusinessViolationCodes.BatchSkuMismatch, nameof(batchId), $"That batch is not one of {medicine.Name}'s batches held at the clinic dispensary."));
        }

        if (drawn is { } d && (d <= 0m || (medicine.IsControlled && decimal.Round(d, SurgeryRules.ControlledQuantityDecimals) != d)))
            return (null, Violation(BusinessViolationCodes.InvalidQuantity, nameof(drawn), medicine.IsControlled
                ? "The quantity drawn must be greater than zero, with at most two decimal places for a controlled drug."
                : "The quantity drawn must be greater than zero."));

        if (wasted is { } w && (w < 0m || (medicine.IsControlled && decimal.Round(w, SurgeryRules.ControlledQuantityDecimals) != w)))
            return (null, Violation(BusinessViolationCodes.InvalidQuantity, nameof(wasted), "The quantity wasted cannot be negative, with at most two decimal places for a controlled drug."));

        // 4. A controlled drug: the administering vet, the wastage, the witness -- verified before any write.
        Guid? witnessId = null;
        if (medicine.IsControlled)
        {
            var administeringVet = await staffRepository.GetByIdAsync(administeredById);
            if (administeringVet is null || !administeringVet.RoleIds.Contains(StaffRoles.Vet))
                return (null, Violation(BusinessViolationCodes.AdministeringVetRequired, nameof(administeredById), "A controlled drug is administered by a veterinarian; the register names the administering vet."));

            if (wasted is null)
                return (null, Violation(BusinessViolationCodes.WastageRequired, nameof(wasted), "Record the quantity wasted (0 when none) for a controlled drug."));
            if (drawn is null || drawn <= wasted)
                return (null, Violation(BusinessViolationCodes.InvalidQuantity, nameof(drawn), "A controlled drug entry records a quantity drawn greater than the quantity wasted."));

            if (string.IsNullOrWhiteSpace(witnessIdentifier) || string.IsNullOrWhiteSpace(witnessPassword))
                return (null, Violation(BusinessViolationCodes.WitnessRequired, "WitnessIdentifier", PharmacyMessages.WitnessRequired));

            var witness = await witnessVerifier.VerifyAsync(witnessIdentifier, witnessPassword, callerId, ControlledDrugSourceType.Surgery, CancellationToken.None);
            if (!witness.IsVerified || witness.WitnessStaffId is null)
                return (null, Violation(BusinessViolationCodes.WitnessNotVerified, "WitnessIdentifier", PharmacyMessages.WitnessNotVerified));

            if (witness.WitnessStaffId == administeredById)
                return (null, Violation(BusinessViolationCodes.WitnessSameAsAdministrator, "WitnessIdentifier", "The witness must be a different person from the administering vet."));

            witnessId = witness.WitnessStaffId;
        }

        return (new DrugPlan(medicine, batch?.SkuId, batch, witnessId, drawn, wasted), null);
    }

    /// <summary>Takes <paramref name="quantity"/> from the named batch. Expired, quarantined or recalled: refused with no override (BR-3, FR-IMP-9). Not enough on hand: the drug has already been
    /// given, so the shortfall is RECORDED (a reorder signal), not refused -- the clinical record must exist.</summary>
    public static async Task<(bool Shortfall, CommandResponse? Refusal)> IssueAsync(
        IStockLedger ledger, SurgeryAggregate surgery, DrugPlan plan, decimal quantity, Guid entryId, string? fefoOverrideReason)
    {
        var batch = plan.Batch!;
        var result = await ledger.DeductAsync(
            new StockDemand(surgery.BranchId, batch.SkuId, quantity, MovementKind.ClinicalUse, null, [(batch.BatchId, batch.LocationId, quantity)], fefoOverrideReason?.Trim()),
            new StockReference(StockReferenceKind.Surgery, surgery.Id, entryId, $"Surgery {surgery.Id.ToString("N")[..12]}"),
            new StockParty(surgery.CustomerId, surgery.CatId), CancellationToken.None);

        if (result.IsSuccess) return (false, null);

        if (result.ViolationCode == BusinessViolationCodes.InsufficientStock)
        {
            ledger.RecordShortfall(new StockShortfall(batch.SkuId, batch.LocationId, quantity, surgery.Id, entryId));
            return (true, null);
        }

        var code = result.ViolationCode == BusinessViolationCodes.BatchExpired ? BusinessViolationCodes.BatchNotUsable : result.ViolationCode!;
        return (false, Violation(code, "BatchId", result.Message ?? "The stock could not be taken."));
    }

    public static Guid EnlistRegister(
        IControlledDrugRegister register, SurgeryAggregate surgery, DrugPlan plan, ControlledDrugEntryKind kind, decimal delta, decimal? wastage, Guid administeredById, Guid entryId,
        DateTimeOffset occurredOn, string doseText, Guid? correctsRegisterEntryId, string? correctionReason)
    {
        var medicine = plan.Medicine;
        var batch = plan.Batch;
        return register.Enlist(new ControlledDrugRegisterEntryDto(
            surgery.BranchId, medicine.MedicineId, medicine.Name, medicine.Strength.Text(), medicine.Form, medicine.ControlledSchedule, medicine.DispensingUnit, plan.SkuId, batch?.BatchId,
            batch?.LotNumber, batch?.ExpiresOn, kind, delta, doseText, wastage, surgery.CatId, surgery.CustomerId, administeredById, plan.WitnessId,
            ControlledDrugSourceType.Surgery, surgery.Id, correctsRegisterEntryId, correctionReason, null, occurredOn, $"Anaesthesia log entry {entryId:N}"));
    }

    /// <summary>The drug values of an entry, with the outcomes of the ports (register entry, shortfall) given by the caller.</summary>
    public static AnaesthesiaDrugDto DrugDto(
        DrugPlan plan, decimal doseAmount, DoseUnit doseUnit, AdministrationRoute route, Guid administeredById, decimal? drawn, decimal? wasted, Guid? registerEntryId, bool shortfall) =>
        new(plan.Medicine.MedicineId, plan.Medicine.Name, plan.Medicine.IsControlled, doseAmount, doseUnit, route, administeredById,
            plan.SkuId, plan.Batch?.BatchId, plan.Batch?.LotNumber, plan.Batch?.LocationId, drawn, wasted, plan.Medicine.DispensingUnit.DisplayName, plan.WitnessId, registerEntryId, shortfall);

    /// <summary>
    /// Runs the entry's own checks (dose, quantities, time window, controlled-drug shape) on a THROWAWAY aggregate BEFORE any port enlists a ledger or register row, so a refusal can never
    /// leave tracked rows behind (CODE_REVIEW row 18). The outcomes of the ports are not known yet, so a controlled drug is given a placeholder register entry id. Returns the violation, or null.
    /// </summary>
    public static CommandResponse? DryRun(Action<AnaesthesiaLogEntryAggregate> apply)
    {
        try
        {
            apply(new AnaesthesiaLogEntryAggregate(Guid.NewGuid()));
            return null;
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }
    }

    public static string DoseText(decimal dose, DoseUnit unit, AdministrationRoute route) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{dose:0.###} {unit.DisplayName} {route.DisplayName}");
}

/// <summary>
/// HO-SRG-001 §8.9 / §11.4 C8 (US-SRG-04, US-SRG-08, ADR-163, ADR-164) -- log a drug. Everything is decided BEFORE anything is written, then committed in ONE save: the log entry, the batch-specific
/// stock issue (HO-IMP-001's ledger, enlisted), and for a controlled drug the witnessed register entries (HO-PHM-001, enlisted) -- all three or none. An expired, quarantined or recalled
/// batch is refused with no override; a stock shortfall is recorded, not refused (the drug has been given); the witness is verified BEFORE any write and must be someone other than the
/// administering vet and the caller.
/// </summary>
public sealed class LogAnaesthesiaDrugCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<AnaesthesiaLogEntryAggregate> entryRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IFormularyReader formulary,
    IStockLedger ledger,
    IControlledDrugRegister register,
    IStaffWitnessVerifier witnessVerifier,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<LogAnaesthesiaDrugCommand>
{
    public async Task<CommandResponse> HandleAsync(LogAnaesthesiaDrugCommand command)
    {
        var user = command.UserContext!;
        var now = timeProvider.GetUtcNow();

        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        if (!await IsVetAsync(staffRepository, user.UserId))
            return Violation(BusinessViolationCodes.VeterinarianRequired, nameof(command.UserContext), "Only a veterinarian logs drugs in the anaesthesia log.");

        // Idempotency (CODE_REVIEW row 17): the entry id is the client's idempotency key. A repeat of a request that already succeeded (a double click, a retry after a lost response) finds
        // its entry and returns the FIRST result -- no second entry, no second deduction, no second register entry. It is answered even if the surgery has since closed. Only the same
        // caller's drug entry on this surgery replays; any other holder of the id is refused, so an id cannot be used to probe another record.
        var existing = await entryRepository.GetByIdAsync(command.EntryId);
        if (existing is not null)
        {
            if (existing.SurgeryId != surgery.Id || existing.Kind != AnaesthesiaEntryKind.Drug || existing.LoggedBy != user.UserId)
                return Violation(BusinessViolationCodes.DuplicateAnaesthesiaEntry, nameof(command.EntryId), SurgeryMessages.DuplicateAnaesthesiaEntry);

            // The same id with a materially different body is not a retry: refuse it rather than silently answer with the first entry (backlog 42).
            if (existing.MedicineId != command.MedicineId || existing.BatchId != command.BatchId || existing.QuantityDrawn != command.QuantityDrawn || existing.DoseAmount != command.DoseAmount)
                return Violation(BusinessViolationCodes.AnaesthesiaEntryIdMismatch, nameof(command.EntryId), SurgeryMessages.AnaesthesiaEntryIdMismatch);

            return Success(new LogDrugResult(existing.Id, existing.RegisterEntryId, existing.StockShortfall));
        }

        if (!AnaesthesiaDrugSupport.IsOpenForLogging(surgery)) return AnaesthesiaDrugSupport.Closed();

        var doseUnit = AppointmentMapper.ParseEnum<DoseUnit>(command.DoseUnit);
        var route = AppointmentMapper.ParseEnum<AdministrationRoute>(command.Route);

        var (plan, refusal) = await AnaesthesiaDrugSupport.PlanAsync(
            surgery, command.MedicineId, command.AdministeredById, command.BatchId, command.QuantityDrawn, command.QuantityWasted, command.WitnessIdentifier, command.WitnessPassword,
            user.UserId, formulary, ledger, staffRepository, witnessVerifier);
        if (refusal is not null) return refusal;
        plan = plan!;

        // The entry's own checks run FIRST, on a throwaway aggregate, so a bad dose or date is refused before any ledger or register row is enlisted (row 18).
        var context = AnaesthesiaDrugSupport.Context(surgery, command.OccurredOn, now, user, command.CorrelationId);
        var placeholderRegisterId = plan.Medicine.IsControlled ? Guid.NewGuid() : (Guid?)null;
        var invalid = AnaesthesiaDrugSupport.DryRun(probe => probe.LogDrug(context, AnaesthesiaDrugSupport.DrugDto(
            plan, command.DoseAmount, doseUnit, route, command.AdministeredById, command.QuantityDrawn, command.QuantityWasted, placeholderRegisterId, false)));
        if (invalid is not null) return invalid;

        var entry = new AnaesthesiaLogEntryAggregate(command.EntryId);
        var occurredOn = command.OccurredOn.ToUniversalTime();

        // 5. Stock, then the register -- both enlisted, neither saved.
        var shortfall = false;
        if (plan.Batch is not null)
        {
            var (isShortfall, stockRefusal) = await AnaesthesiaDrugSupport.IssueAsync(ledger, surgery, plan, command.QuantityDrawn!.Value, entry.Id, command.FefoOverrideReason);
            if (stockRefusal is not null) return stockRefusal;
            shortfall = isShortfall;
        }

        Guid? registerEntryId = null;
        if (plan.Medicine.IsControlled)
        {
            var wasted = command.QuantityWasted!.Value;
            var administered = command.QuantityDrawn!.Value - wasted;
            var doseText = AnaesthesiaDrugSupport.DoseText(command.DoseAmount, doseUnit, route);

            try
            {
                registerEntryId = AnaesthesiaDrugSupport.EnlistRegister(register, surgery, plan, ControlledDrugEntryKind.Administered, -administered, null, command.AdministeredById, entry.Id, occurredOn, doseText, null, null);
                if (wasted > 0m)
                    AnaesthesiaDrugSupport.EnlistRegister(register, surgery, plan, ControlledDrugEntryKind.Wastage, -wasted, wasted, command.AdministeredById, entry.Id, occurredOn, doseText, null, null);
            }
            catch (DomainRuleViolationException ex)
            {
                return Violation(ex);
            }
        }

        try
        {
            entry.LogDrug(context, AnaesthesiaDrugSupport.DrugDto(
                plan, command.DoseAmount, doseUnit, route, command.AdministeredById, command.QuantityDrawn, command.QuantityWasted, registerEntryId, shortfall));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        // ONE save: the entry, the ledger entry, the register entries and any shortfall alert commit together (F23). If anything throws, none of the three exists.
        await entryRepository.SaveAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success(new LogDrugResult(entry.Id, registerEntryId, shortfall));
    }
}

/// <summary>The controller-facing result of C8 (§12.4): <c>201 { entryId, registerEntryId?, stockShortfall }</c>.</summary>
public sealed record LogDrugResult(Guid EntryId, Guid? RegisterEntryId, bool StockShortfall);

/// <summary>HO-SRG-001 §11.4 C9 (US-SRG-04) -- vitals: at least one value, outside the feline plausibility ranges only with an explicit confirmation (which is stored). A nurse or a vet.</summary>
public sealed class LogAnaesthesiaVitalsCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository, IAggregateRootRepository<AnaesthesiaLogEntryAggregate> entryRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<LogAnaesthesiaVitalsCommand>
{
    public async Task<CommandResponse> HandleAsync(LogAnaesthesiaVitalsCommand command)
    {
        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);
        if (!AnaesthesiaDrugSupport.IsOpenForLogging(surgery)) return AnaesthesiaDrugSupport.Closed();

        var entry = new AnaesthesiaLogEntryAggregate(command.EntryId);
        try
        {
            entry.LogVitals(
                AnaesthesiaDrugSupport.Context(surgery, command.OccurredOn, timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId),
                new AnaesthesiaVitalsDto(command.HeartRateBpm, command.RespiratoryRatePerMin, command.SpO2Percent, command.EtCo2MmHg, command.TemperatureCelsius, command.SystolicMmHg,
                    command.MeanArterialMmHg, command.Note, command.ConfirmImplausible));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await entryRepository.SaveAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success(entry.Id);
    }
}

/// <summary>HO-SRG-001 §11.4 C9 (US-SRG-04) -- a milestone (premedication, induction, intubation, incision start, procedure end, extubation, other).</summary>
public sealed class LogAnaesthesiaMilestoneCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository, IAggregateRootRepository<AnaesthesiaLogEntryAggregate> entryRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<LogAnaesthesiaMilestoneCommand>
{
    public async Task<CommandResponse> HandleAsync(LogAnaesthesiaMilestoneCommand command)
    {
        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);
        if (!AnaesthesiaDrugSupport.IsOpenForLogging(surgery)) return AnaesthesiaDrugSupport.Closed();

        var entry = new AnaesthesiaLogEntryAggregate(command.EntryId);
        try
        {
            entry.LogMilestone(
                AnaesthesiaDrugSupport.Context(surgery, command.OccurredOn, timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId),
                new AnaesthesiaMilestoneDto(AppointmentMapper.ParseEnum<AnaesthesiaMilestone>(command.Milestone), command.Note));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await entryRepository.SaveAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success(entry.Id);
    }
}

/// <summary>
/// HO-SRG-001 §8.9 / §11.4 C10 (US-SRG-04, US-SRG-08 AC2) -- correct an entry. The target must exist on THIS surgery and be the latest of its chain (a correction of a correction corrects the
/// original's latest state). A drug correction needs a vet; its stock follows the change in quantity (a further issue for a positive delta, a RETURN for a negative one, same batch) and a
/// controlled drug writes a NEW register entry of kind Correction linked to the original register entry, with a NEW witness. The original entry, its ledger entry and its register entry are
/// untouched. Nothing is edited or deleted anywhere.
/// </summary>
public sealed class CorrectAnaesthesiaEntryCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<AnaesthesiaLogEntryAggregate> entryRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IFormularyReader formulary,
    IStockLedger ledger,
    IControlledDrugRegister register,
    IStaffWitnessVerifier witnessVerifier,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CorrectAnaesthesiaEntryCommand>
{
    public async Task<CommandResponse> HandleAsync(CorrectAnaesthesiaEntryCommand command)
    {
        var user = command.UserContext!;
        var now = timeProvider.GetUtcNow();

        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);
        if (!AnaesthesiaDrugSupport.IsOpenForLogging(surgery)) return AnaesthesiaDrugSupport.Closed();

        var target = await entryRepository.GetByIdAsync(command.CorrectsEntryId);
        if (target is null || target.SurgeryId != surgery.Id)
            return Violation(BusinessViolationCodes.CorrectionTargetNotFound, nameof(command.CorrectsEntryId), "That log entry does not exist on this surgery.");

        if (await entryRepository.ExistAsync(e => e.CorrectsEntryId == target.Id))
            return Violation(BusinessViolationCodes.CorrectionTargetNotFound, nameof(command.CorrectsEntryId), "That entry has already been corrected. Correct the latest correction instead.");

        var correctedKind = target.Kind == AnaesthesiaEntryKind.Correction ? target.CorrectedKind! : target.Kind;
        var payloads = new[] { command.Drug is not null, command.Vitals is not null, command.Milestone is not null }.Count(x => x);
        if (payloads != 1 || (correctedKind == AnaesthesiaEntryKind.Drug) != (command.Drug is not null)
            || (correctedKind == AnaesthesiaEntryKind.Vitals) != (command.Vitals is not null) || (correctedKind == AnaesthesiaEntryKind.Milestone) != (command.Milestone is not null))
            return Violation(BusinessViolationCodes.InvalidAnaesthesiaEntry, nameof(command.CorrectsEntryId), $"Send the corrected {correctedKind.DisplayName.ToLowerInvariant()} values for that entry.");

        // Who may correct what: a drug is a vet's (like logging it); vitals and milestones are a nurse's or a vet's (the endpoint admits both).
        if (correctedKind == AnaesthesiaEntryKind.Drug && !await IsVetAsync(staffRepository, user.UserId))
            return Violation(BusinessViolationCodes.VeterinarianRequired, nameof(command.UserContext), "Only a veterinarian corrects a drug entry.");

        var entry = new AnaesthesiaLogEntryAggregate(command.EntryId);
        var context = AnaesthesiaDrugSupport.Context(surgery, command.OccurredOn, now, user, command.CorrelationId);

        try
        {
            if (command.Vitals is { } v)
            {
                entry.Correct(context, new AnaesthesiaCorrectionDto(target.Id, correctedKind, command.Reason, null,
                    new AnaesthesiaVitalsDto(v.HeartRateBpm, v.RespiratoryRatePerMin, v.SpO2Percent, v.EtCo2MmHg, v.TemperatureCelsius, v.SystolicMmHg, v.MeanArterialMmHg, v.Note, v.ConfirmImplausible), null));
            }
            else if (command.Milestone is { } m)
            {
                entry.Correct(context, new AnaesthesiaCorrectionDto(target.Id, correctedKind, command.Reason, null, null,
                    new AnaesthesiaMilestoneDto(AppointmentMapper.ParseEnum<AnaesthesiaMilestone>(m.Milestone), m.Note)));
            }
            else
            {
                var drugResult = await CorrectDrugAsync(surgery, target, entry, context, command, user);
                if (drugResult is not null) return drugResult;
            }
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await entryRepository.SaveAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success(entry.Id);
    }

    /// <summary>Returns a refusal, or <c>null</c> when the entry has been built and its stock and register entries enlisted (the caller saves).</summary>
    private async Task<CommandResponse?> CorrectDrugAsync(
        SurgeryAggregate surgery, AnaesthesiaLogEntryAggregate target, AnaesthesiaLogEntryAggregate entry, AnaesthesiaEntryContext context,
        CorrectAnaesthesiaEntryCommand command, Platform.Infrastructure.Common.Security.UserContext user)
    {
        var drug = command.Drug!;
        var doseUnit = AppointmentMapper.ParseEnum<DoseUnit>(drug.DoseUnit);
        var route = AppointmentMapper.ParseEnum<AdministrationRoute>(drug.Route);
        var medicineId = target.MedicineId!.Value;

        // The batch of the entry being corrected stays the batch (a batch change is a return of the whole original plus a NEW entry).
        var (plan, refusal) = await AnaesthesiaDrugSupport.PlanAsync(
            surgery, medicineId, drug.AdministeredById, target.BatchId, drug.QuantityDrawn, drug.QuantityWasted, command.WitnessIdentifier, command.WitnessPassword,
            user.UserId, formulary, ledger, staffRepository, witnessVerifier);
        if (refusal is not null) return refusal;
        plan = plan!;

        // Everything the entry itself can refuse is checked BEFORE any ledger or register row is enlisted (row 18): the register link, then the entry's own rules on a throwaway aggregate.
        if (plan.Medicine.IsControlled && target.RegisterEntryId is null)
            return Violation(BusinessViolationCodes.CorrectionTargetNotFound, nameof(command.CorrectsEntryId), "That entry has no register entry to correct.");

        var invalid = AnaesthesiaDrugSupport.DryRun(probe => probe.Correct(context, new AnaesthesiaCorrectionDto(target.Id, AnaesthesiaEntryKind.Drug, command.Reason,
            AnaesthesiaDrugSupport.DrugDto(plan, drug.DoseAmount, doseUnit, route, drug.AdministeredById, drug.QuantityDrawn, drug.QuantityWasted, plan.Medicine.IsControlled ? Guid.NewGuid() : null, false), null, null)));
        if (invalid is not null) return invalid;

        var oldDrawn = target.QuantityDrawn ?? 0m;
        var newDrawn = drug.QuantityDrawn ?? 0m;
        var delta = newDrawn - oldDrawn;
        var shortfall = false;

        if (plan.Batch is not null && delta > 0m)
        {
            var (isShortfall, stockRefusal) = await AnaesthesiaDrugSupport.IssueAsync(ledger, surgery, plan, delta, entry.Id, command.FefoOverrideReason);
            if (stockRefusal is not null) return stockRefusal;
            shortfall = isShortfall;
        }
        else if (plan.Batch is not null && delta < 0m)
        {
            var returned = await ledger.ReceiveReturnAsync(
                new StockReturn(surgery.BranchId, plan.Batch.SkuId, plan.Batch.BatchId, -delta, "Anaesthesia log correction"),
                new StockReference(StockReferenceKind.Surgery, surgery.Id, entry.Id, $"Surgery {surgery.Id.ToString("N")[..12]} correction"),
                new StockParty(surgery.CustomerId, surgery.CatId), CancellationToken.None);
            if (!returned.IsSuccess)
                return Violation(returned.ViolationCode!, "QuantityDrawn", returned.Message ?? "The stock could not be returned.");
        }

        Guid? registerEntryId = null;
        if (plan.Medicine.IsControlled)
        {
            var doseText = AnaesthesiaDrugSupport.DoseText(drug.DoseAmount, doseUnit, route);
            try
            {
                registerEntryId = AnaesthesiaDrugSupport.EnlistRegister(
                    register, surgery, plan, ControlledDrugEntryKind.Correction, -delta, drug.QuantityWasted, drug.AdministeredById, entry.Id, command.OccurredOn.ToUniversalTime(), doseText,
                    target.RegisterEntryId, command.Reason.Trim());
            }
            catch (DomainRuleViolationException ex)
            {
                return Violation(ex);
            }
        }

        entry.Correct(context, new AnaesthesiaCorrectionDto(target.Id, AnaesthesiaEntryKind.Drug, command.Reason,
            AnaesthesiaDrugSupport.DrugDto(plan, drug.DoseAmount, doseUnit, route, drug.AdministeredById, drug.QuantityDrawn, drug.QuantityWasted, registerEntryId, shortfall), null, null));
        return null;
    }
}
