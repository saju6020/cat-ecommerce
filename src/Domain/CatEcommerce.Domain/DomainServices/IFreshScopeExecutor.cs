namespace CatEcommerce.Domain.DomainServices;

/// <summary>Runs work against a service resolved from a new DI scope (a new DbContext). Implemented in Infrastructure over <c>IServiceScopeFactory</c>.</summary>
public interface IFreshScopeExecutor
{
    Task<TResult> ExecuteAsync<TService, TResult>(Func<TService, Task<TResult>> work) where TService : notnull;

    /// <summary>Forgets everything the REQUEST scope DbContext is tracking (entities and outbox rows of a lost first attempt), so nothing later in the request can save and replay it.</summary>
    void DiscardRequestScopeChanges();
}
