using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 backlog row 8: "today" for age maths is the business date -- the calendar date in
/// the main branch's zone (as the care-reminder sweep does), not the UTC date.</summary>
internal static class ShopBusinessDate
{
    private const string FallbackZone = "Asia/Dhaka";

    public static async Task<DateOnly> TodayAsync(CatEcommerceDbContext db, TimeProvider timeProvider)
    {
        var zone = await db.Set<BranchAggregate>().AsNoTracking()
            .Where(b => b.Id == AdministrationSeed.MainBranchId)
            .Select(b => b.TimeZoneId)
            .FirstOrDefaultAsync();
        return BranchCalendar.LocalDateOf(timeProvider.GetUtcNow(), zone ?? FallbackZone);
    }
}
