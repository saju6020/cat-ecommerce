using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>
/// HO-ORD-001 §8.5 / OQ-5 / U-A4 (ADR-198): Ordering computes in <c>long</c> minor units and stores ADR-054's <c>decimal(12,2)</c>, which is
/// lossless only for a two-digit currency. A <c>commerce.currencyCode</c> with any other digit count is refused <b>at start-up</b> rather than
/// silently mis-scaled at the first checkout. A settings read that fails for infrastructure reasons (the database is not reachable yet) is logged
/// and does not block start-up: the refusal is for a wrong <i>configuration</i>, not for a slow dependency.
/// </summary>
public sealed class CurrencyStartupGuard(IServiceScopeFactory scopeFactory, ILogger<CurrencyStartupGuard> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        string currency;
        try
        {
            using var scope = scopeFactory.CreateScope();
            currency = scope.ServiceProvider.GetRequiredService<IBusinessSettings>().GetString(SettingKeys.CommerceCurrencyCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The Ordering currency check could not read commerce.currencyCode; start-up continues.");
            return Task.CompletedTask;
        }

        Check(currency);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>The refusal itself, separated so it is unit-testable without a host.</summary>
    public static void Check(string currencyCode)
    {
        if (!CurrencyMinorDigits.IsSupported(currencyCode))
            throw new InvalidOperationException(
                $"commerce.currencyCode '{currencyCode}' is not supported by Ordering: prices are computed in two-decimal minor units (HO-ORD-001 ADR-198, OQ-5). " +
                "Use a two-digit currency, or amend ADR-198 first.");
    }
}
