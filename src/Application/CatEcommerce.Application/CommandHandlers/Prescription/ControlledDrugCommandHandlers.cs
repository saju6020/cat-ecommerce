using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Prescription.PharmacyHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Prescription;

/// <summary>
/// HO-PHM-001 §11.4 C7 (FR-PHM-7, ADR-144) -- a correction to the register: a NEW entry linked to the one it corrects; the original stays exactly as it was.
/// Order matters: the caller is a vet, the target is in the active branch, and the WITNESS is verified (their own identifier and password) BEFORE anything is
/// written; then, when the quantity changes, an Inventory <c>Adjustment</c> is enlisted through <see cref="IStockLedger.AdjustAsync"/> so stock and register stay in
/// step, and the entry is saved -- both commit in ONE save (F23). The witness password is on the command only.
/// </summary>
public sealed class RecordControlledDrugCorrectionCommandHandler(
    IAggregateRootRepository<ControlledDrugRegisterEntryAggregate> entryRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IStaffWitnessVerifier witnessVerifier,
    IStockLedger ledger,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordControlledDrugCorrectionCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordControlledDrugCorrectionCommand command)
    {
        var ct = CancellationToken.None;
        var user = command.UserContext!;

        var caller = await staffRepository.GetByIdAsync(user.UserId);
        if (caller is null || !caller.RoleIds.Contains(StaffRoles.Vet))
            return Violation(BusinessViolationCodes.PrescribingNotPermitted, nameof(command.UserContext), "Only a veterinarian may correct the controlled-drug register.");

        var target = await entryRepository.GetByIdAsync(command.CorrectsEntryId);
        if (target is null)
            return Violation(BusinessViolationCodes.RegisterEntryNotFound, nameof(command.CorrectsEntryId), "Register entry not found.");

        if (target.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.CorrectionTargetMismatch, nameof(command.CorrectsEntryId), "That entry belongs to another branch's register.");

        if (string.IsNullOrWhiteSpace(command.Reason))
            return Violation(BusinessViolationCodes.CorrectionReasonRequired, nameof(command.Reason), "A correction needs a reason.");

        // The witness, BEFORE any write.
        var witness = await witnessVerifier.VerifyAsync(command.WitnessIdentifier, command.WitnessPassword, user.UserId, ControlledDrugSourceType.Correction, ct);
        if (!witness.IsVerified)
            return Violation(BusinessViolationCodes.WitnessNotVerified, nameof(command.WitnessIdentifier), PharmacyMessages.WitnessNotVerified);

        var now = timeProvider.GetUtcNow();
        var catId = command.CorrectedCatId ?? target.CatId;
        var customerId = target.CustomerId;
        if (command.CorrectedCatId is { } correctedCatId)
        {
            var cat = await catRepository.GetByIdAsync(correctedCatId);
            if (cat is null) return Violation(BusinessViolationCodes.CatNotFound, nameof(command.CorrectedCatId), "Cat not found.");
            customerId = cat.OwnerId;
        }

        var entryDto = new ControlledDrugRegisterEntryDto(
            target.BranchId, target.MedicineId, target.MedicineName, target.MedicineStrengthText, target.MedicineForm, target.MedicineControlledSchedule,
            target.MedicineDispensingUnit, target.SkuId, target.BatchId, target.LotNumber, target.BatchExpiresOn, ControlledDrugEntryKind.Correction,
            command.QuantityDelta, command.CorrectedDoseText ?? target.DoseText, null, catId, customerId, user.UserId, witness.WitnessStaffId,
            ControlledDrugSourceType.Correction, target.Id, target.Id, command.Reason.Trim(), null, now, command.Notes);

        ControlledDrugRegisterEntryAggregate entry;
        try { entry = ControlledDrugRegisterEntryAggregate.Record(entryDto, new RegisterStamp(now, command.ToPharmacyActor())); }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        // A quantity change also moves stock, so register and stock stay in step -- enlisted, not saved, then committed with the entry.
        if (command.QuantityDelta != 0m && target.SkuId is { } skuId && target.BatchId is { } batchId)
        {
            var locationId = await ResolveDispensaryAsync(target.BranchId, skuId, batchId, ct);
            if (locationId is null)
                return Violation(BusinessViolationCodes.MedicineNotStocked, nameof(command.QuantityDelta), "This branch has no clinic dispensary to adjust.");

            var adjusted = await ledger.AdjustAsync(
                new StockAdjustment(target.BranchId, skuId, batchId, locationId.Value, command.QuantityDelta, $"CD register correction #{target.EntryNumber}", ApprovedByStaffId: null),
                new StockReference(StockReferenceKind.ControlledDrugCorrection, entry.Id, null, $"CD correction #{target.EntryNumber}"),
                new StockParty(null, null), ct);

            if (!adjusted.IsSuccess)
                return Violation(adjusted.ViolationCode!, adjusted.PropertyName ?? nameof(command.QuantityDelta), adjusted.Message ?? "The stock could not be adjusted.");
        }

        await entryRepository.SaveAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success(new RecordedRegisterEntry(entry.Id, entry.EntryNumber));
    }

    private async Task<Guid?> ResolveDispensaryAsync(Guid branchId, Guid skuId, Guid batchId, CancellationToken ct)
    {
        var batches = await ledger.GetDispensaryBatchesAsync(branchId, [skuId], ct);
        var held = batches.IsSuccess
            ? batches.Value!.Where(c => c.BatchId == batchId).OrderByDescending(c => c.Free).FirstOrDefault()
            : null;
        if (held is not null) return held.LocationId;

        var dispensary = await ledger.GetClinicDispensaryAsync(branchId, ct);
        return dispensary.IsSuccess ? dispensary.Value!.LocationId : null;
    }
}

/// <summary>
/// HO-PHM-001 §11.4 C8 (ADR-145) -- the sealing pass for ONE branch: unsealed entries recorded more than <c>SealSettleSeconds</c> ago, in entry order, at most
/// <c>SealBatchSize</c>; each seal chains from the previous hash (the all-zero hash at position 1). All seals commit in one save; if another worker sealed the
/// same positions first the pass writes nothing and reports success -- the other worker won. Idempotent.
/// </summary>
public sealed class SealControlledDrugEntriesCommandHandler(IRegisterChain chain, TimeProvider timeProvider)
    : ICommandHandlerAsync<SealControlledDrugEntriesCommand>
{
    public async Task<CommandResponse> HandleAsync(SealControlledDrugEntriesCommand command)
    {
        var ct = CancellationToken.None;
        var now = timeProvider.GetUtcNow();

        var unsealed = await chain.GetUnsealedAsync(command.BranchId, now.AddSeconds(-ControlledDrugRules.SealSettleSeconds), ControlledDrugRules.SealBatchSize, ct);
        if (unsealed.Count == 0) return Success(new SealingResult(0, false));

        var tip = await chain.GetTipAsync(command.BranchId, ct);
        var position = (tip?.ChainPosition ?? 0) + 1;
        var previousHash = tip?.EntryHash ?? ControlledDrugRules.ZeroHash;

        var seals = new List<ControlledDrugRegisterSealAggregate>(unsealed.Count);
        foreach (var entry in unsealed)
        {
            var seal = ControlledDrugRegisterSealAggregate.Seal(entry, position++, previousHash, now, command.ToPharmacyActor());
            previousHash = seal.EntryHash;
            seals.Add(seal);
        }

        var written = await chain.TryAppendAsync(seals, ct);
        return Success(new SealingResult(written ? seals.Count : 0, !written));
    }
}

/// <summary>An export of the register for inspection is itself audited (HO-PHM-001 §8.9): who, which branch, which filters, how many rows -- never the content.</summary>
public sealed class RecordControlledDrugRegisterExportCommandHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository, IClientIpAccessor clientIpAccessor, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordControlledDrugRegisterExportCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordControlledDrugRegisterExportCommand command)
    {
        var user = command.UserContext!;
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "ControlledDrugRegister", EntityId: command.BranchId, Action: "ControlledDrugRegisterExported", Reason: "ControlledDrugRegisterExported",
            ActorUserId: user.UserId, OccurredOn: timeProvider.GetUtcNow(), BeforeValue: null,
            AfterValue: $"MedicineId={command.MedicineId};From={command.From:yyyy-MM-dd};To={command.To:yyyy-MM-dd};Rows={command.RowCount}",
            UserContext: user, CorrelationId: command.CorrelationId, Category: AuditCategory.BusinessEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
        return Success();
    }
}
