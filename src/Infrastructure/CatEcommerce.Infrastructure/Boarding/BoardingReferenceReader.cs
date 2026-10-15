using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Vaccination;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Boarding;

/// <summary>
/// HO-BRD-001 ADR-232 (renumbered ADR-365): Boarding's read-only window onto Administration -- a branch's time zone and state and a tax class's state --
/// so no Boarding command handler injects an Administration repository (U-X4). Untracked, projected, async.
/// </summary>
public sealed class BoardingReferenceReader(CatEcommerceDbContext db) : IBoardingReferenceReader
{
    public Task<BoardingBranchInfo?> GetBranchAsync(Guid branchId, CancellationToken ct = default) =>
        db.Set<BranchAggregate>().AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => new BoardingBranchInfo(b.Id, b.TimeZoneId, b.IsActive))
            .FirstOrDefaultAsync(ct);

    public Task<bool> TaxClassIsActiveAsync(Guid taxClassId, CancellationToken ct = default) =>
        db.Set<TaxClassAggregate>().AsNoTracking().AnyAsync(t => t.Id == taxClassId && t.IsActive, ct);

    public async Task<IReadOnlyList<VaccinationRuleVaccine>> GetActiveCoreVaccinesAsync(CancellationToken ct = default) =>
        await db.Set<VaccineAggregate>().AsNoTracking()
            .Where(v => v.IsCore && v.IsActive)
            .Select(v => new VaccinationRuleVaccine(v.Id, v.Code, v.Name))
            .ToListAsync(ct);

    public async Task<DateOnly?> LocalDateAsync(Guid branchId, DateTimeOffset instant, CancellationToken ct = default)
    {
        var zone = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == branchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync(ct);
        return zone is null ? null : BoardingOccupancyWindow.LocalDateOf(instant, zone);
    }
}
