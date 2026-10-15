using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>
/// HO-ORD-001 §12.7 / ADR-202 -- a singleton <see cref="IDbContextFactory{TContext}"/> for the edge infrastructure that must commit <b>on its own</b>
/// (the idempotency claim, the webhook inbox): a fresh <see cref="CatEcommerceDbContext"/> per call, built from the same connection string as
/// the request-scoped context. It deliberately carries <b>no audit interceptors</b> -- those need the caller's user context and audit aggregate
/// changes, and an idempotency row is neither -- and it is the only way these writes avoid riding on the request's own unit of work.
/// </summary>
public sealed class OpsDbContextFactory(IConfiguration configuration) : IDbContextFactory<CatEcommerceDbContext>
{
    public CatEcommerceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CatEcommerceDbContext>()
            .UseNpgsql(configuration.GetConnectionString("CatEcommerce"))
            .Options;

        return new CatEcommerceDbContext(options);
    }
}
