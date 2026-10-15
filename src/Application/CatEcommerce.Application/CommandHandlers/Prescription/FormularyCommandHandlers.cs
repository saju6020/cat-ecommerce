using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Prescription.PharmacyHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Prescription;

/// <summary>
/// HO-PHM-001 §11.4 C1-C4 (US-PHM-04). A uniqueness or existence check is a repository call in the handler (rule 5); every other rule lives in the
/// aggregate. The linked SKUs are validated through <see cref="ICatalogueReader"/> (they exist and are active); that a SKU is linked to ONE medicine is a
/// handler pre-check backed by the unique index on <c>formulary_medicine_sku.sku_id</c>.
/// </summary>
internal static class FormularyChecks
{
    public static async Task<CommandResponse?> CheckReferencesAsync(
        FormularyMedicineCommandBase command, IAggregateRootRepository<FormularyMedicineAggregate> repository, ICatalogueReader catalogue)
    {
        var skuIds = command.LinkedSkuIds.Distinct().ToList();
        if (skuIds.Count > 0)
        {
            var skus = await catalogue.GetSkusAsync(skuIds, CancellationToken.None);
            if (skus.Count != skuIds.Count || skus.Any(s => !s.IsActive))
                return Violation(BusinessViolationCodes.SkuNotFound, nameof(command.LinkedSkuIds), "One of the linked SKUs does not exist or is not active.");

            var medicineId = command.MedicineId;
            if (await repository.ExistAsync(m => m.Id != medicineId && m.LinkedSkus.Any(l => skuIds.Contains(l.SkuId))))
                return Violation(BusinessViolationCodes.SkuAlreadyLinkedToMedicine, nameof(command.LinkedSkuIds), "One of those SKUs is already linked to another medicine.");
        }

        foreach (var otherId in command.Interactions.Where(i => i.InteractsWithMedicineId is not null).Select(i => i.InteractsWithMedicineId!.Value).Distinct())
        {
            var id = otherId;
            if (!await repository.ExistAsync(m => m.Id == id))
                return Violation(BusinessViolationCodes.InvalidInteraction, nameof(command.Interactions), "An interaction names a medicine that is not in the formulary.");
        }

        return null;
    }
}

public sealed class CreateFormularyMedicineCommandHandler(
    IAggregateRootRepository<FormularyMedicineAggregate> repository, ICatalogueReader catalogue)
    : ICommandHandlerAsync<CreateFormularyMedicineCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateFormularyMedicineCommand command)
    {
        if (await repository.ExistAsync(m => m.Code == command.Code))
            return Violation(BusinessViolationCodes.FormularyCodeAlreadyExists, nameof(command.Code), $"A medicine with code '{command.Code}' already exists. Choose another code.");

        if (await FormularyChecks.CheckReferencesAsync(command, repository, catalogue) is { } refusal) return refusal;

        var medicine = new FormularyMedicineAggregate(command.MedicineId);
        try { medicine.Create(command.Code, command.ToDomainDto()); }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        await repository.SaveAsync(medicine, EventPublishOption.InMemoryPublishOnly);
        return Success(medicine.Id);
    }
}

public sealed class UpdateFormularyMedicineCommandHandler(
    IAggregateRootRepository<FormularyMedicineAggregate> repository, ICatalogueReader catalogue)
    : ICommandHandlerAsync<UpdateFormularyMedicineCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateFormularyMedicineCommand command)
    {
        var medicine = await repository.GetByIdAsync(command.MedicineId);
        if (medicine is null)
            return Violation(BusinessViolationCodes.FormularyMedicineNotFound, nameof(command.MedicineId), "Medicine not found.");

        if (medicine.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleVersion, nameof(command.ExpectedVersion), "This medicine has changed since you loaded it. Reload and try again.");

        if (await FormularyChecks.CheckReferencesAsync(command, repository, catalogue) is { } refusal) return refusal;

        try { medicine.Update(command.ToDomainDto()); }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        await repository.UpdateAsync(medicine, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class SetFormularyMedicineControlledCommandHandler(
    IAggregateRootRepository<FormularyMedicineAggregate> repository, IControlledDrugDirectory directory)
    : ICommandHandlerAsync<SetFormularyMedicineControlledCommand>
{
    public async Task<CommandResponse> HandleAsync(SetFormularyMedicineControlledCommand command)
    {
        var medicine = await repository.GetByIdAsync(command.MedicineId);
        if (medicine is null)
            return Violation(BusinessViolationCodes.FormularyMedicineNotFound, nameof(command.MedicineId), "Medicine not found.");

        // Only clearing the flag needs to know whether the register already holds entries for the medicine (the history stays; the change must be explicable).
        var hasRegisterEntries = !command.IsControlled && medicine.IsControlled && await directory.HasRegisterEntriesAsync(medicine.Id, CancellationToken.None);

        try { medicine.SetControlled(command.IsControlled, command.ControlledSchedule, command.Reason, hasRegisterEntries, command.ToPharmacyActor()); }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        await repository.UpdateAsync(medicine, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class SetFormularyMedicineActiveCommandHandler(IAggregateRootRepository<FormularyMedicineAggregate> repository)
    : ICommandHandlerAsync<SetFormularyMedicineActiveCommand>
{
    public async Task<CommandResponse> HandleAsync(SetFormularyMedicineActiveCommand command)
    {
        var medicine = await repository.GetByIdAsync(command.MedicineId);
        if (medicine is null)
            return Violation(BusinessViolationCodes.FormularyMedicineNotFound, nameof(command.MedicineId), "Medicine not found.");

        medicine.SetActive(command.IsActive, command.ToPharmacyActor());
        await repository.UpdateAsync(medicine, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
