using System.Globalization;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>
/// HO-ADM-001 §8.9 / §12.2. Loads both scopes (global, branch override), caches 30 s in
/// <see cref="IMemoryCache"/>, invalidated by <c>InvalidateSettingsCacheHandler</c> on a
/// <c>BusinessSettingsChangedEvent</c> (§11.6) so a change is visible on the next request.
///
/// <see cref="IBusinessSettings"/>'s four original methods are synchronous (HO-ADM-001 §8.9's own
/// signature, already depended on by earlier consumers -- HO-ACC-001's idle timeout, HO-NTF-001's
/// quiet hours). Resolving a value therefore reads the database synchronously; the 30 s cache keeps
/// that rare rather than per-request.
/// </summary>
public sealed class CachedBusinessSettings(CatEcommerceDbContext db, IMemoryCache cache) : IBusinessSettings
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public int GetInt(string key, Guid? branchId = null) =>
        int.Parse(Resolve(key, branchId), NumberStyles.Integer, CultureInfo.InvariantCulture);

    public decimal GetDecimal(string key, Guid? branchId = null) =>
        decimal.Parse(Resolve(key, branchId), NumberStyles.Number, CultureInfo.InvariantCulture);

    public bool GetBool(string key, Guid? branchId = null) => bool.Parse(Resolve(key, branchId));

    public TimeRange GetTimeRange(string key, Guid? branchId = null)
    {
        var parts = Resolve(key, branchId).Split('-');
        return new TimeRange(
            TimeOnly.Parse(parts[0], CultureInfo.InvariantCulture),
            TimeOnly.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    public string GetString(string key, Guid? branchId = null) => Resolve(key, branchId);

    /// <summary>Resolution order (§8.7): branch override -> global value -> the definition's default.</summary>
    private string Resolve(string key, Guid? branchId)
    {
        var definition = SettingDefinitions.Find(key)
            ?? throw new InvalidOperationException($"'{key}' is not a registered setting.");

        if (branchId is { } id && id != SettingScopes.GlobalId)
        {
            var branchValue = GetScopeValues(id).GetValueOrDefault(key);
            if (branchValue is not null) return branchValue;
        }

        var globalValue = GetScopeValues(SettingScopes.GlobalId).GetValueOrDefault(key);
        return globalValue ?? definition.Default;
    }

    private IReadOnlyDictionary<string, string> GetScopeValues(Guid scopeId)
    {
        var cacheKey = CacheKeyFor(scopeId);

        if (cache.TryGetValue<IReadOnlyDictionary<string, string>>(cacheKey, out var cached) && cached is not null)
            return cached;

        var values = db.Set<BusinessSettingsAggregate>()
            .AsNoTracking()
            .Where(s => s.Id == scopeId)
            .SelectMany(s => s.Values)
            .ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);

        cache.Set(cacheKey, (IReadOnlyDictionary<string, string>)values, CacheDuration);
        return values;
    }

    private static string CacheKeyFor(Guid scopeId) => $"business-settings:{scopeId}";

    /// <summary>Called by <c>InvalidateSettingsCacheHandler</c> (via <see cref="ISettingsCacheInvalidator"/>)
    /// so a change is visible immediately.</summary>
    public static void Invalidate(IMemoryCache cache, Guid scopeId) => cache.Remove(CacheKeyFor(scopeId));
}

/// <summary>The Infrastructure-side implementation of <see cref="ISettingsCacheInvalidator"/> --
/// the Application-layer event handler depends only on the Domain port.</summary>
public sealed class MemoryCacheSettingsCacheInvalidator(IMemoryCache cache) : ISettingsCacheInvalidator
{
    public void Invalidate(Guid scopeId) => CachedBusinessSettings.Invalidate(cache, scopeId);
}

/// <summary>
/// HO-ACC-001 §12.2's rewire: <c>security.staffIdleTimeoutMinutes</c> now comes from
/// <see cref="IBusinessSettings"/>; <c>AccountOptions.StaffIdleTimeoutMinutes</c> survives only as
/// the registry's seed default (already the value <c>SettingDefinitions.Administration.cs</c>
/// hard-codes, 30). The other two lifetimes have no setting yet, so they delegate to the same
/// <c>AccountOptions</c> <see cref="AccountOptionsSessionPolicy"/> already reads.
/// </summary>
public sealed class BusinessSettingsSessionPolicy(IBusinessSettings settings, IOptions<AccountOptions> accountOptions) : ISessionPolicy
{
    public TimeSpan CustomerSessionLifetime => TimeSpan.FromHours(accountOptions.Value.CustomerSessionLifetimeHours);
    public TimeSpan StaffSessionLifetime => TimeSpan.FromHours(accountOptions.Value.StaffSessionLifetimeHours);
    public TimeSpan StaffIdleTimeout => TimeSpan.FromMinutes(settings.GetInt(SettingKeys.SecurityStaffIdleTimeoutMinutes));
}
