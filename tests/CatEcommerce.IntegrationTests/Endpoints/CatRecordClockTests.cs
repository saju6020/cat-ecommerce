using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>CatRecordClock against the real database. Each test changes the main branch inside a transaction
/// that is rolled back, so no other test ever sees the change.</summary>
public sealed class CatRecordClockTests(CatApiFactory factory) : CatIntegrationTestBase(factory)
{
    // 2026-10-10 20:00Z: 2026-10-11 02:00 in Dhaka, but still 2026-10-10 in New York (UTC-4).
    private static readonly FakeTimeProvider Time = new(DateTimeOffset.Parse("2026-10-10T20:00:00Z"));

    [Fact]
    public async Task LocalTodayAsync_MainBranchHasNonDefaultZone_UsesThatZone()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE admin.branch SET time_zone_id = 'America/New_York' WHERE \"Id\" = {0}", AdministrationSeed.MainBranchId);

        var today = await new CatRecordClock(db, Time).LocalTodayAsync(CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 10, 10), today);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task LocalTodayAsync_MainBranchMissing_FallsBackToDhaka()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        // HO-XCT-001 ADR-299: a retained branch row cannot be deleted (not even in a transaction that is rolled back), so "missing" is simulated
        // by moving the main branch to another id for the duration of this rolled-back transaction.
        await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL DEFERRED");
        await db.Database.ExecuteSqlRawAsync("UPDATE admin.branch SET \"Id\" = {0} WHERE \"Id\" = {1}", Guid.NewGuid(), AdministrationSeed.MainBranchId);

        var today = await new CatRecordClock(db, Time).LocalTodayAsync(CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 10, 11), today);
        await tx.RollbackAsync();
    }
}
