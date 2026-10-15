using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>The main branch's calendar date for cat records; see <see cref="ICatRecordClock"/>.</summary>
public sealed class CatRecordClock(CatEcommerceDbContext db, TimeProvider timeProvider) : ICatRecordClock
{
    private const string FallbackZone = "Asia/Dhaka";

    public async Task<DateOnly> LocalTodayAsync(CancellationToken ct)
    {
        var zone = await db.Branches.AsNoTracking()
            .Where(b => b.Id == AdministrationSeed.MainBranchId)
            .Select(b => b.TimeZoneId)
            .FirstOrDefaultAsync(ct);
        return BranchCalendar.LocalDateOf(timeProvider.GetUtcNow(), zone ?? FallbackZone);
    }
}
