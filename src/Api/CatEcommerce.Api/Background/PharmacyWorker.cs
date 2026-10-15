using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-PHM-001 §12.2 -- the same shape and rules as HO-APT-001's <c>ClinicWorker</c> and HO-IMP-001's <c>InventoryWorker</c>: a scope per poll, never throws out of
/// <c>ExecuteAsync</c>, disabled in <c>Testing</c> (<c>Pharmacy:Worker:Enabled=false</c>); tests call <see cref="PharmacyWorkCycle"/> directly. A throwing pass is logged
/// and the next poll still runs (U-X4). <b>Nothing safety-relevant waits for this worker</b>: a diet locks and an expired prescription is refused the moment its date
/// passes (ADR-142, ADR-149) -- the worker only seals the register, records expiries, raises events and sends reminders.
/// </summary>
public sealed class PharmacyWorker(IServiceScopeFactory scopeFactory, IOptions<PharmacyOptions> options, ILogger<PharmacyWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var worker = options.Value.Worker;
        if (!worker.Enabled)
        {
            logger.LogInformation("PharmacyWorker is disabled (Pharmacy:Worker:Enabled=false).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PharmacyWorkCycle>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "PharmacyWorker pass failed; will retry on the next poll.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, worker.IntervalSeconds)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }
}
