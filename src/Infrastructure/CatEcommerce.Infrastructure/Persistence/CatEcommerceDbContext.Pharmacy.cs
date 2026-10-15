using CatEcommerce.Domain.Aggregates.Prescription;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>Pharmacy and Prescriptions (HO-PHM-001) DbSets -- one partial per area (ADR-341), added phase by phase. The model is discovered from the
/// configurations; these properties are the typed entry points.</summary>
public sealed partial class CatEcommerceDbContext
{
    // Phase B -- the formulary
    public DbSet<FormularyMedicineAggregate> FormularyMedicines => Set<FormularyMedicineAggregate>();

    // Phase C -- prescriptions (with their warnings and repeat requests)
    public DbSet<PrescriptionAggregate> Prescriptions => Set<PrescriptionAggregate>();

    // Phase D -- the controlled-drug register and its hash-chain seals (append-only; see ControlledDrugRegisterImmutabilityInterceptor and the triggers)
    public DbSet<ControlledDrugRegisterEntryAggregate> ControlledDrugRegisterEntries => Set<ControlledDrugRegisterEntryAggregate>();
    public DbSet<ControlledDrugRegisterSealAggregate> ControlledDrugRegisterSeals => Set<ControlledDrugRegisterSealAggregate>();

    // Phase E -- dispensing (its own root, committed with the prescription's supply count, the ledger entries and the register entries in one save)
    public DbSet<DispensingAggregate> Dispensings => Set<DispensingAggregate>();

    // Phase G -- diet authorisations (ADR-149)
    public DbSet<DietAuthorisationAggregate> DietAuthorisations => Set<DietAuthorisationAggregate>();
}
