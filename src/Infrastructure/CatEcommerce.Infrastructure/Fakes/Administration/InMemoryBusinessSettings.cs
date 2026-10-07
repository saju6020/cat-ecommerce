using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Infrastructure.Fakes.Administration;

/// <summary>
/// FAKE until HO-ADM-001 lands -- replace this registration.
///
/// Simple in-memory settings source, not fail-closed: there is no business rule to deny, only
/// settings that do not exist yet. Takes an optional dictionary of overrides (keyed
/// <c>"{key}"</c> globally or <c>"{key}:{branchId}"</c> for a branch override) for tests that need
/// a specific value; every other key returns the type's zero value.
/// </summary>
public sealed class InMemoryBusinessSettings(IReadOnlyDictionary<string, object>? overrides = null) : IBusinessSettings
{
    private readonly IReadOnlyDictionary<string, object> _overrides = overrides ?? new Dictionary<string, object>();

    public int GetInt(string key, Guid? branchId = null) => Get(key, branchId, 0);

    public decimal GetDecimal(string key, Guid? branchId = null) => Get(key, branchId, 0m);

    public bool GetBool(string key, Guid? branchId = null) => Get(key, branchId, false);

    public TimeRange GetTimeRange(string key, Guid? branchId = null) =>
        Get(key, branchId, new TimeRange(TimeOnly.MinValue, TimeOnly.MinValue));

    public string GetString(string key, Guid? branchId = null) => Get(key, branchId, "");

    private T Get<T>(string key, Guid? branchId, T fallback)
    {
        if (branchId is { } id && _overrides.TryGetValue($"{key}:{id}", out var scoped) && scoped is T scopedValue)
            return scopedValue;

        return _overrides.TryGetValue(key, out var global) && global is T globalValue ? globalValue : fallback;
    }
}
