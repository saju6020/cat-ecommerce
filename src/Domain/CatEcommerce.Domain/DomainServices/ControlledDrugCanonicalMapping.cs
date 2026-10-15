using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-PHM-001 §8.9 -- maps a register entry to the primitives its seal is hashed over. Kept off the aggregate on purpose: the entry has exactly one
/// method (its factory) so that nothing on it can look like a way to change it (U-D3).
/// </summary>
public static class ControlledDrugCanonicalMapping
{
    public static ControlledDrugCanonicalFields From(ControlledDrugRegisterEntryAggregate e) => new(
        e.Id, e.EntryNumber, e.BranchId, e.MedicineId, e.MedicineName, e.MedicineStrengthText, e.MedicineForm.DisplayName, e.MedicineControlledSchedule,
        e.MedicineDispensingUnit.DisplayName, e.SkuId, e.BatchId, e.LotNumber, e.BatchExpiresOn, e.Kind.DisplayName, e.QuantityDelta, e.DoseText,
        e.WastageQuantity, e.CatId, e.CustomerId, e.ActorId, e.WitnessId, e.SourceType.DisplayName, e.SourceId, e.CorrectsEntryId, e.CorrectionReason,
        e.ReturnedEntryId, e.OccurredOn, e.RecordedOn, e.Notes);
}
