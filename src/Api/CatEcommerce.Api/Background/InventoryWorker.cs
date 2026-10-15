using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-IMP-001 section 12.2 -- the same shape and rules as HO-NTF-001's <c>NotificationWorker</c> and HO-SHP-001's <c>ShopWorker</c>: a scope per poll,
/// never throws out of <c>ExecuteAsync</c>, disabled in <c>Testing</c> (<c>Inventory:Worker:Enabled=false</c>); tests call <see cref="InventoryWorkCycle"/>
/// directly. A throwing pass is logged and the next poll still runs (U-X3).
/// </summary>
public sealed class InventoryWorker(IServiceScopeFactory scopeFactory, IOptions<InventoryOptions> options, ILogger<InventoryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var worker = options.Value.Worker;
        if (!worker.Enabled)
        {
            logger.LogInformation("InventoryWorker is disabled (Inventory:Worker:Enabled=false).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var workCycle = scope.ServiceProvider.GetRequiredService<InventoryWorkCycle>();
                await workCycle.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "InventoryWorker pass failed; will retry on the next poll.");
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
