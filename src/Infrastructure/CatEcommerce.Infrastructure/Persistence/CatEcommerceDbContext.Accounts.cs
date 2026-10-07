using CatEcommerce.Domain.Aggregates.Account;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>
/// Accounts and Identity (HO-ACC-001) DbSets. See ADR-341 for the per-area partial pattern.
///
/// <b>The two audit interceptors (§11.9, §12.1) are wired in <c>Program.cs</c>'s single
/// <c>AddDbContext</c> call, not here.</b> An earlier revision of this file added a second public
/// constructor (taking <c>IClientIpAccessor</c>/<c>IUserContextProvider</c>) plus an
/// <c>OnConfiguring</c> override, reasoning that ASP.NET Core's <c>AddDbContext</c> resolves
/// whichever public constructor it can satisfy from the current scope. That worked, but it made
/// audit capture -- including the immutability interceptor -- fail **open**: anything that ever
/// constructed a <c>CatEcommerceDbContext</c> through its simpler, options-only constructor
/// (design-time tooling, a future test double, a direct <c>new</c>) silently got no interceptors
/// at all, because the `is not null` guard in that removed `OnConfiguring` skipped them without
/// so much as a log line. Wiring both interceptors directly into the `AddDbContext((sp, o) => ...)`
/// options delegate in `Program.cs` closes that gap structurally: every
/// `CatEcommerceDbContext` this application ever resolves through DI is built by that one
/// delegate, so there is no second, interceptor-less code path left to fall into (ARCHITECTURE
/// gate on HO-ACC-001, §12.1: "Register the two interceptors via `AddInterceptors` in the
/// `AddDbContext` call. Nothing else changes."). This partial file now carries only what is
/// actually Accounts-specific -- its seven `DbSet`s.
/// </summary>
public sealed partial class CatEcommerceDbContext
{
    public DbSet<CustomerAccountAggregate> CustomerAccounts => Set<CustomerAccountAggregate>();
    public DbSet<CustomerSessionAggregate> CustomerSessions => Set<CustomerSessionAggregate>();
    public DbSet<PasswordResetTokenAggregate> PasswordResetTokens => Set<PasswordResetTokenAggregate>();
    public DbSet<CustomerAddressBookAggregate> CustomerAddressBooks => Set<CustomerAddressBookAggregate>();
    public DbSet<CustomerNotificationPreferencesAggregate> CustomerNotificationPreferences => Set<CustomerNotificationPreferencesAggregate>();
    public DbSet<StaffAccountAggregate> StaffAccounts => Set<StaffAccountAggregate>();
    public DbSet<StaffSessionAggregate> StaffSessions => Set<StaffSessionAggregate>();
}
