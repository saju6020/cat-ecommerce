using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Pharmacy;

/// <summary>HO-PHM-001 §11.6 (ADR-152) -- ids only, projected in SQL (no aggregate is rehydrated to find them). Register entries are deliberately absent: they are never rewritten.</summary>
public sealed class EfPharmacyRecordFinder(CatEcommerceDbContext db) : IPharmacyRecordFinder
{
    public async Task<IReadOnlyList<Guid>> FindPrescriptionIdsAsync(Guid customerId, CancellationToken ct) =>
        await db.Prescriptions.AsNoTracking().Where(p => p.CustomerId == customerId).Select(p => p.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindDispensingIdsAsync(Guid customerId, CancellationToken ct) =>
        await db.Dispensings.AsNoTracking().Where(d => d.CustomerId == customerId).Select(d => d.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindDietAuthorisationIdsAsync(Guid customerId, CancellationToken ct) =>
        await db.DietAuthorisations.AsNoTracking().Where(a => a.CustomerId == customerId).Select(a => a.Id).ToListAsync(ct);
}
