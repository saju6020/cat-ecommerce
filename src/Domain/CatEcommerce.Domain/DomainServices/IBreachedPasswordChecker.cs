namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-ACC-001 §8.10. Local list only (ADR-027, OQ-6) -- no network call.</summary>
public interface IBreachedPasswordChecker
{
    Task<bool> IsBreachedAsync(string plaintext, CancellationToken ct = default);
}
