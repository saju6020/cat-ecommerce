namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ACC-001 §12.2. A small port over <c>AccountOptions</c> (Infrastructure) so Application
/// handlers -- which reference only Domain and Shared (`.ai/cqrs.md` rule 2) -- can read the
/// configured session lifetimes and staff idle timeout without a forbidden Infrastructure
/// reference.
/// </summary>
public interface ISessionPolicy
{
    TimeSpan CustomerSessionLifetime { get; }
    TimeSpan StaffSessionLifetime { get; }
    TimeSpan StaffIdleTimeout { get; }
}
