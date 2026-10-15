using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CatEcommerce.Api.Hosting;

/// <summary>
/// HO-ORD-001-FU1 ORD-54 (ADR-217) -- reports <b>Degraded</b> (HTTP 200 on <c>/health</c>, so a probe does not restart the host) and logs a warning when the
/// <c>DELIVERY</c> tax class is missing, inactive or has no rate: delivery orders cannot be quoted until an Administrator fixes it. Reads through the
/// published <see cref="IOrderBillingReader"/>; writes nothing, and the description carries no data.
/// </summary>
public sealed class DeliveryTaxClassHealthCheck(IServiceScopeFactory scopes, TimeProvider time, ILogger<DeliveryTaxClassHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var facts = await scope.ServiceProvider.GetRequiredService<IOrderBillingReader>().GetDeliveryTaxAsync(time.GetUtcNow(), cancellationToken);
            if (facts.TaxClassId is not null) return HealthCheckResult.Healthy();

            logger.LogWarning("The {ClassCode} tax class is missing, inactive or has no rate: delivery orders cannot be quoted.", DeliveryTaxFacts.ClassCode);
            return HealthCheckResult.Degraded($"The {DeliveryTaxFacts.ClassCode} tax class is not usable; delivery orders cannot be quoted.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The {ClassCode} tax class check could not run.", DeliveryTaxFacts.ClassCode);
            return HealthCheckResult.Degraded("The delivery tax class check could not run.");
        }
    }
}
