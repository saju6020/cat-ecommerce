using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>
/// HO-IMP-001 §8.13 -- the branch's "today". Loads the branch's IANA zone and asks HO-ADM-001's <c>BranchCalendar.LocalDateOf</c> for the
/// local date, so zone conversion stays in the calendar services (ADR-045). A branch that no longer exists falls back to the seeded
/// <c>Asia/Dhaka</c> zone rather than failing a stock read.
/// </summary>
public sealed class InventoryClock(CatEcommerceDbContext db, TimeProvider timeProvider) : IInventoryClock
{
    private const string FallbackZone = "Asia/Dhaka";

    public async Task<string> TimeZoneIdAsync(Guid branchId, CancellationToken ct) =>
        await db.Branches.AsNoTracking().Where(b => b.Id == branchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync(ct) ?? FallbackZone;

    public async Task<DateOnly> LocalTodayAsync(Guid branchId, CancellationToken ct) =>
        BranchCalendar.LocalDateOf(timeProvider.GetUtcNow(), await TimeZoneIdAsync(branchId, ct));

    public async Task<DateOnly> LocalDateOfAsync(Guid branchId, DateTimeOffset instant, CancellationToken ct) =>
        BranchCalendar.LocalDateOf(instant, await TimeZoneIdAsync(branchId, ct));
}
