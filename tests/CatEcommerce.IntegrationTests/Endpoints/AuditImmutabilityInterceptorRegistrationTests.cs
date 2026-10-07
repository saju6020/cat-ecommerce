using CatEcommerce.Infrastructure.Auditing;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-ACC-001 ARCHITECTURE gate (MEDIUM): the two audit interceptors are wired in Program.cs's
/// <c>AddDbContext</c> options delegate, not via a second constructor + <c>OnConfiguring</c> on
/// the Accounts partial -- so they cannot fail open for whatever <c>CatEcommerceDbContext</c> the
/// application actually resolves through DI. These tests resolve the context exactly the way the
/// application does (from a DI scope, via <see cref="AccountsApiFactory"/>'s real host) and prove
/// <see cref="AuditEntryImmutabilityInterceptor"/> is active on it -- an EF <c>SaveChangesAsync</c>
/// against a tracked, modified <c>AuditEntryAggregate</c> throws
/// <see cref="AuditEntryImmutableException"/> **before** any SQL reaches the database, which is a
/// different, narrower guarantee than <c>AuditImmutabilityTests</c>' raw-SQL tests prove (those
/// exercise the database trigger layer only, since raw SQL never passes through EF's change
/// tracker or its interceptors at all).
/// </summary>
public sealed class AuditImmutabilityInterceptorRegistrationTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    [Fact]
    public async Task DbContextResolvedFromDi_HasTheImmutabilityInterceptorActive()
    {
        using var client = CreateClient();
        var (customerId, _, _) = await RegisterCustomerAsync(client);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        // Tracked (no AsNoTracking), then marked Modified directly on the change tracker --
        // AuditEntryAggregate has no public mutator to call (§8.11: "no other public method may
        // exist"), which is itself part of what this interceptor exists to backstop, so forcing
        // the tracked state is the only way to exercise it without going through raw SQL.
        var entry = await db.AuditEntries.FirstAsync(e => e.EntityId == customerId);
        db.Entry(entry).State = EntityState.Modified;

        var exception = await Record.ExceptionAsync(() => db.SaveChangesAsync());

        Assert.IsType<AuditEntryImmutableException>(exception);
    }
}
