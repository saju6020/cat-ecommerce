using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Boarding;

/// <summary>HO-BRD-001 F34 / section 8.20: rostered caretakers' shift instants for a branch and range -- <see cref="StaffAvailability"/> unrestricted by opening hours.</summary>
public sealed class BoardingCareInputs(CatEcommerceDbContext db) : IBoardingCareInputs
{
    public async Task<IReadOnlyList<CaretakerShifts>> GetCaretakerShiftsAsync(Guid branchId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var zone = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == branchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync(ct);
        if (zone is null) return [];

        var staffIds = await db.Set<StaffBranchAssignmentAggregate>().AsNoTracking()
            .Where(a => a.BranchIds.Contains(branchId)).Select(a => a.Id).ToListAsync(ct);

        var result = new List<CaretakerShifts>();
        foreach (var staffId in staffIds)
        {
            var staff = await db.Set<StaffAccountAggregate>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId, ct);
            if (staff is null || !staff.RoleIds.Contains(StaffRoles.Caretaker)) continue;

            var roster = await db.Set<StaffRosterAggregate>().AsNoTracking().FirstOrDefaultAsync(r => r.StaffAccountId == staffId && r.BranchId == branchId, ct);
            if (roster is null) continue;

            var exceptions = await db.Set<RosterExceptionAggregate>().AsNoTracking()
                .Where(e => e.StaffAccountId == staffId && e.BranchId == branchId && !e.IsCancelled && e.Date >= from && e.Date <= to).ToListAsync(ct);
            var leave = await db.Set<LeaveRequestAggregate>().AsNoTracking()
                .Where(l => l.StaffAccountId == staffId && l.Status == LeaveStatus.Approved && l.StartsOn <= to && l.EndsOn >= from).ToListAsync(ct);

            var intervals = StaffAvailability.Intervals(roster, exceptions, leave, [], zone, from, to, restrictToOpeningHours: false);
            result.Add(new CaretakerShifts(staffId, intervals));
        }

        return result;
    }
}

public sealed class BoardingTaskFinder(CatEcommerceDbContext db) : IBoardingTaskFinder
{
    public async Task<IReadOnlyList<Guid>> FindPendingAsync(Guid stayId, Guid? catId, DateOnly fromLocalDate, CancellationToken ct = default) =>
        await db.BoardingCareTasks.AsNoTracking()
            .Where(t => t.StayId == stayId && t.Status == CareTaskStatus.Pending && t.LocalDate >= fromLocalDate && (catId == null || t.CatId == catId))
            .Select(t => t.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindPendingAssignedToAsync(Guid caretakerId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await db.BoardingCareTasks.AsNoTracking()
            .Where(t => t.AssignedCaretakerId == caretakerId && t.Status == CareTaskStatus.Pending && t.LocalDate >= from && t.LocalDate <= to)
            .Select(t => t.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindPendingInRangeAsync(Guid stayId, Guid catId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
        await db.BoardingCareTasks.AsNoTracking()
            .Where(t => t.StayId == stayId && t.CatId == catId && t.Status == CareTaskStatus.Pending && t.DueOn >= from && t.DueOn <= to)
            .Select(t => t.Id).ToListAsync(ct);
}

public sealed class BoardingStayFinder(CatEcommerceDbContext db) : IBoardingStayFinder
{
    public async Task<IReadOnlyList<Guid>> FindLiveByCatAsync(Guid catId, CancellationToken ct = default) =>
        await db.BoardingStays.AsNoTracking()
            .Where(s => (s.Status == BoardingStayStatus.Reserved || s.Status == BoardingStayStatus.Confirmed || s.Status == BoardingStayStatus.CheckedIn) && s.Cats.Any(c => c.CatId == catId))
            .Select(s => s.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await db.BoardingStays.AsNoTracking().Where(s => s.CustomerId == customerId).Select(s => s.Id).ToListAsync(ct);

    public Task<Guid?> FindInHouseByCatAsync(Guid catId, CancellationToken ct = default) =>
        db.BoardingStays.AsNoTracking().Where(s => s.Status == BoardingStayStatus.CheckedIn && s.Cats.Any(c => c.CatId == catId)).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindOnUnitOverlappingAsync(Guid unitId, DateTimeOffset from, DateTimeOffset to, Guid excludeStayId, CancellationToken ct = default) =>
        await db.BoardingStays.AsNoTracking()
            .Where(s => s.Id != excludeStayId && (s.Status == BoardingStayStatus.Reserved || s.Status == BoardingStayStatus.Confirmed || s.Status == BoardingStayStatus.CheckedIn)
                        && s.Allocations.Any(a => a.IsActive && a.UnitId == unitId && a.OccupiedFrom < to && from < a.OccupiedTo))
            .Select(s => s.Id).ToListAsync(ct);

    public Task<Guid?> FindByReferralAsync(Guid referralId, CancellationToken ct = default) =>
        db.BoardingStays.AsNoTracking().Where(s => s.Referrals.Any(r => r.Id == referralId)).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindCheckedInAsync(CancellationToken ct = default) =>
        await db.BoardingStays.AsNoTracking().Where(s => s.Status == BoardingStayStatus.CheckedIn).Select(s => s.Id).ToListAsync(ct);
}
