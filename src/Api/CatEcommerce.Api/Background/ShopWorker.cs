using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-SHP-001 section 12.2 -- the same shape and rules as HO-NTF-001's <c>NotificationWorker</c> and
/// HO-APT-001's <c>ClinicWorker</c>: a scope per unit of work, never throws out of
/// <c>ExecuteAsync</c>, disabled in <c>Testing</c> (<c>Shop:Worker:Enabled=false</c>); tests call
/// <see cref="ShopWorkCycle"/> directly. One pass notifies every Pending stock alert whose SKU is
/// available again (U-X2: a throwing pass is logged and the next one still runs).
/// </summary>
public sealed class ShopWorker(IServiceScopeFactory scopeFactory, IOptions<ShopOptions> options, ILogger<ShopWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var worker = options.Value.Worker;
        if (!worker.Enabled)
        {
            logger.LogInformation("ShopWorker is disabled (Shop:Worker:Enabled=false).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var workCycle = scope.ServiceProvider.GetRequiredService<ShopWorkCycle>();
                await workCycle.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "ShopWorker pass failed; will retry on the next poll.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(worker.IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }
}
