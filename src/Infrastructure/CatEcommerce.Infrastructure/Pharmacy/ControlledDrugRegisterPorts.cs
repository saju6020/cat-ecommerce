using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Infrastructure.Pharmacy;

/// <summary>
/// HO-PHM-001 §8.9 / §12.2 (ADR-144) -- builds the register entry and ADDS it to the scoped <see cref="CatEcommerceDbContext"/> WITHOUT saving: exactly
/// <c>EfNotificationOutbox</c>'s shape (F23). The caller's own repository save commits it with the business change, so an entry exists if and only if the thing
/// it records was committed. A rule of the entry table (missing witness, wrong sign, same actor and witness) is a <c>DomainRuleViolationException</c> from the
/// aggregate. Registered Scoped.
/// </summary>
public sealed class EfControlledDrugRegister(CatEcommerceDbContext db, IUserContextProvider userContextProvider, TimeProvider timeProvider) : IControlledDrugRegister
{
    public Guid Enlist(ControlledDrugRegisterEntryDto entry)
    {
        var stamp = new RegisterStamp(timeProvider.GetUtcNow(), new PharmacyActor(userContextProvider.GetUserContext(), Guid.NewGuid()));
        var aggregate = ControlledDrugRegisterEntryAggregate.Record(entry, stamp);
        db.ControlledDrugRegisterEntries.Add(aggregate);
        return aggregate.Id;
    }
}

/// <summary>HO-PHM-001 §8.9 -- which SKUs and medicines are controlled, read from the formulary. Read-only. Also answers whether the register already holds an
/// entry for a medicine (the controlled-flag change rule).</summary>
public sealed class ControlledDrugDirectory(CatEcommerceDbContext db) : IControlledDrugDirectory
{
    public async Task<ControlledMedicineRef?> FindBySkuAsync(Guid skuId, CancellationToken ct) =>
        await FindAsync(db.FormularyMedicines.Where(m => m.IsControlled && m.LinkedSkus.Any(l => l.SkuId == skuId)), ct);

    public async Task<ControlledMedicineRef?> FindByMedicineAsync(Guid medicineId, CancellationToken ct) =>
        await FindAsync(db.FormularyMedicines.Where(m => m.IsControlled && m.Id == medicineId), ct);

    public Task<bool> HasRegisterEntriesAsync(Guid medicineId, CancellationToken ct) =>
        db.ControlledDrugRegisterEntries.AsNoTracking().AnyAsync(e => e.MedicineId == medicineId, ct);

    private static async Task<ControlledMedicineRef?> FindAsync(IQueryable<FormularyMedicineAggregate> medicines, CancellationToken ct)
    {
        var found = (await medicines.ToSnapshotsAsync(ct)).FirstOrDefault();
        return found is null
            ? null
            : new ControlledMedicineRef(found.MedicineId, found.Name, found.Strength.Text(), found.Form, found.ControlledSchedule, found.DispensingUnit, found.LinkedSkuIds);
    }
}
