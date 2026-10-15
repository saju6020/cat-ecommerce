using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Boarding;

public sealed class BoardingWaitlistDaycareFinder(CatEcommerceDbContext db) : IBoardingWaitlistDaycareFinder
{
    public async Task<IReadOnlySet<int>> GetTakenDaycareSlotsAsync(Guid branchId, DateOnly localDate, CancellationToken ct = default) =>
        (await db.DaycareVisits.AsNoTracking().Where(v => v.BranchId == branchId && v.LocalDate == localDate && v.Status != DaycareVisitStatus.Cancelled).Select(v => v.SlotNumber).ToListAsync(ct)).ToHashSet();

    public Task<Guid?> FindDaycareRateCardInForceAsync(Guid branchId, DateTimeOffset instant, CancellationToken ct = default) =>
        db.DaycareRateCards.AsNoTracking().Where(c => c.BranchId == branchId && c.EffectiveFrom <= instant).OrderByDescending(c => c.EffectiveFrom).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);

    public Task<bool> CatHasDaycareVisitAsync(Guid catId, DateOnly localDate, CancellationToken ct = default) =>
        db.DaycareVisits.AsNoTracking().AnyAsync(v => v.CatId == catId && v.LocalDate == localDate && v.Status != DaycareVisitStatus.Cancelled, ct);

    public Task<int> CountLiveWaitlistAsync(Guid customerId, CancellationToken ct = default) =>
        db.BoardingWaitlistEntries.AsNoTracking().CountAsync(e => e.CustomerId == customerId && (e.Status == BoardingWaitlistStatus.Waiting || e.Status == BoardingWaitlistStatus.Notified), ct);

    public Task<int> CountFutureDaycareVisitsAsync(Guid customerId, DateOnly fromDate, CancellationToken ct = default) =>
        db.DaycareVisits.AsNoTracking().CountAsync(v => v.CustomerId == customerId && v.LocalDate >= fromDate && (v.Status == DaycareVisitStatus.Booked || v.Status == DaycareVisitStatus.CheckedIn), ct);

    public async Task<IReadOnlyList<Guid>> FindWaitlistByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await db.BoardingWaitlistEntries.AsNoTracking().Where(e => e.CustomerId == customerId).Select(e => e.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindDaycareByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await db.DaycareVisits.AsNoTracking().Where(v => v.CustomerId == customerId).Select(v => v.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindWaitingEntriesAsync(CancellationToken ct = default) =>
        await db.BoardingWaitlistEntries.AsNoTracking().Where(e => e.Status == BoardingWaitlistStatus.Waiting).OrderBy(e => e.JoinedOn).Select(e => e.Id).Take(500).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindWaitingEntriesArrivingBeforeAsync(DateOnly utcDateBound, CancellationToken ct = default) =>
        await db.BoardingWaitlistEntries.AsNoTracking().Where(e => e.Status == BoardingWaitlistStatus.Waiting && e.ArrivalOn < utcDateBound).Select(e => e.Id).Take(500).ToListAsync(ct);

    public Task<int> CountNotifiedOverlappingAsync(Guid branchId, DateOnly arrivalOn, DateOnly departureOn, Guid excludeEntryId, CancellationToken ct = default) =>
        db.BoardingWaitlistEntries.AsNoTracking().CountAsync(e =>
            e.BranchId == branchId && e.Id != excludeEntryId && e.Status == BoardingWaitlistStatus.Notified && e.ArrivalOn < departureOn && arrivalOn < e.DepartureOn, ct);

    public Task<int> CountOlderWaitingOverlappingAsync(Guid branchId, DateOnly arrivalOn, DateOnly departureOn, DateTimeOffset joinedBefore, CancellationToken ct = default) =>
        db.BoardingWaitlistEntries.AsNoTracking().CountAsync(e =>
            e.BranchId == branchId && e.Status == BoardingWaitlistStatus.Waiting && e.JoinedOn < joinedBefore && e.ArrivalOn < departureOn && arrivalOn < e.DepartureOn, ct);

    public async Task<IReadOnlyList<(Guid StayId, DateOnly LocalDate)>> FindDailyUpdateCandidatesAsync(DateTimeOffset now, Func<Guid, int> hourOfBranch, CancellationToken ct = default)
    {
        var stays = await db.BoardingStays.AsNoTracking().Where(s => s.Status == BoardingStayStatus.CheckedIn).Select(s => new { s.Id, s.BranchId }).ToListAsync(ct);
        var zones = await db.Branches.AsNoTracking().Select(b => new { b.Id, b.TimeZoneId }).ToDictionaryAsync(b => b.Id, b => b.TimeZoneId, ct);

        var due = new List<(Guid, DateOnly)>();
        foreach (var stay in stays)
        {
            var zone = zones.GetValueOrDefault(stay.BranchId, "UTC");
            var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(zone));
            if (local.Hour >= hourOfBranch(stay.BranchId)) due.Add((stay.Id, DateOnly.FromDateTime(local.DateTime)));
        }

        return due;
    }
}
