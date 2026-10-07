using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-APT-001 §12.2 -- the same shape and rules as HO-NTF-001's <c>NotificationWorker</c>: a scope
/// per unit of work, never throws out of <c>ExecuteAsync</c>, disabled in <c>Testing</c> (via
/// <c>Clinic:Worker:Enabled</c>). Holds are not expired by this worker -- lazily, by the next write
/// that overlaps them (ADR-070).
/// </summary>
public sealed class ClinicWorker(IServiceScopeFactory scopeFactory, IOptions<ClinicOptions> options, ILogger<ClinicWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var worker = options.Value.Worker;
        if (!worker.Enabled)
        {
            logger.LogInformation("ClinicWorker is disabled (Clinic:Worker:Enabled=false).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var workCycle = scope.ServiceProvider.GetRequiredService<ClinicWorkCycle>();
                await workCycle.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "ClinicWorker pass failed; will retry on the next poll.");
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
