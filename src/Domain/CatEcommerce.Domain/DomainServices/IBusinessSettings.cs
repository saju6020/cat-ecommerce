using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Owned by HO-ADM-001 §8.9. Exact surface from §8.9's own table row. Resolution order (§8.7):
/// branch override → global value → the definition's default. Real implementation
/// (<c>CachedBusinessSettings</c>, 30 s cache, invalidated by <c>BusinessSettingsChangedEvent</c>)
/// is HO-ADM-001's.
///
/// Needed before HO-ADM-001 lands by at least HO-ACC-001 (<c>security.staffIdleTimeoutMinutes</c>,
/// §10.2) and HO-NTF-001 (<c>notifications.quietHours</c>, §8.14) -- both earlier or the same wave
/// as Administration. Until it lands, <c>Fakes/Administration/InMemoryBusinessSettings</c> takes an
/// optional dictionary of overrides in its constructor (for tests) and otherwise returns each
/// type's default (<c>0</c>, <c>0m</c>, <c>false</c>, a zero-length <see cref="TimeRange"/>) -- a
/// simple in-memory fake, not a gate: there is no business rule to fail closed here, only settings
/// that do not exist yet.
/// </summary>
public interface IBusinessSettings
{
    int GetInt(string key, Guid? branchId = null);

    decimal GetDecimal(string key, Guid? branchId = null);

    bool GetBool(string key, Guid? branchId = null);

    TimeRange GetTimeRange(string key, Guid? branchId = null);

    /// <summary>Added by the owning area (HO-ADM-001) for <see cref="Shared.Common.Enums.SettingType.CurrencyCode"/>
    /// (e.g. <c>commerce.currencyCode</c>) -- the registry's fifth type, added after the port's
    /// first four methods were fixed by earlier consumers (ADR-054).</summary>
    string GetString(string key, Guid? branchId = null);
}
