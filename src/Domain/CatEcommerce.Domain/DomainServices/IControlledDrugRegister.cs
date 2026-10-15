using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-PHM-001 §8.9 (ADR-144) -- the contract HO-SRG-001, HO-EMR-001 and HO-IMP-001 write the controlled-drug register through. <see cref="Enlist"/> adds
/// the entry to the scoped DbContext WITHOUT saving: the caller's own repository save commits it with the business change (F23), so an entry exists
/// if and only if the thing it records was committed -- exactly <c>EfNotificationOutbox</c>'s shape. Throws <c>DomainRuleViolationException</c> on a
/// rule of the entry table (missing witness, wrong sign, same actor and witness).
///
/// The CALLER verifies the witness through <see cref="IStaffWitnessVerifier"/> BEFORE enlisting: the register checks that a witness id is present and
/// differs from the actor, but it cannot check a password it was never given. HO-SRG-001's anaesthesia log therefore records a controlled drug as:
/// verify witness, enlist the Inventory movement, enlist the register entry, save its own aggregate.
/// </summary>
public interface IControlledDrugRegister
{
    /// <summary>Returns the new entry's id.</summary>
    Guid Enlist(ControlledDrugRegisterEntryDto entry);
}

/// <summary>HO-PHM-001 §8.9 -- which SKUs and medicines are controlled. Read-only.</summary>
public interface IControlledDrugDirectory
{
    /// <summary>null = the SKU is not a controlled medicine.</summary>
    Task<ControlledMedicineRef?> FindBySkuAsync(Guid skuId, CancellationToken ct);

    Task<ControlledMedicineRef?> FindByMedicineAsync(Guid medicineId, CancellationToken ct);

    /// <summary>Whether the register already holds any entry for the medicine (clearing the controlled flag then needs a reason).</summary>
    Task<bool> HasRegisterEntriesAsync(Guid medicineId, CancellationToken ct);
}

/// <summary>HO-PHM-001 §8.9 -- the formulary as a value, for areas that must not rehydrate a <c>FormularyMedicineAggregate</c> (ADR-151). Read-only.</summary>
public interface IFormularyReader
{
    Task<FormularyMedicineSnapshot?> GetAsync(Guid medicineId, CancellationToken ct);

    Task<FormularyMedicineSnapshot?> FindBySkuAsync(Guid skuId, CancellationToken ct);
}
