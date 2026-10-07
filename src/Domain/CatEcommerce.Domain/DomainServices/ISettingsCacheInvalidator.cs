namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ADM-001 §8.9/§11.6. A small port so the Application-layer domain event handler
/// (<c>InvalidateSettingsCacheHandler</c>) can evict a cached settings scope without a forbidden
/// Application -&gt; Infrastructure reference (Application references only Domain and Shared) --
/// the same reason <c>ISessionPolicy</c> exists (HO-ACC-001 §12.2).
/// </summary>
public interface ISettingsCacheInvalidator
{
    void Invalidate(Guid scopeId);
}
