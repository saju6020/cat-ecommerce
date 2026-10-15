using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.Infrastructure.Boarding;

/// <summary>Scoped: it holds the REQUEST scope DbContext so a lost first attempt can be wiped from it; the retries run in new scopes.</summary>
public sealed class FreshScopeExecutor(IServiceScopeFactory scopeFactory, CatEcommerceDbContext requestDb) : IFreshScopeExecutor
{
    public async Task<TResult> ExecuteAsync<TService, TResult>(Func<TService, Task<TResult>> work) where TService : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public void DiscardRequestScopeChanges() => requestDb.ChangeTracker.Clear();
}
