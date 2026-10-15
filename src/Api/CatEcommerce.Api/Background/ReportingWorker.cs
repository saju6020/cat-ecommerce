using CatEcommerce.Infrastructure.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-RPT-001 section 12.2 -- the same shape and rules as HO-NTF-001's <c>NotificationWorker</c> and HO-ORD-001's <c>OrderWorker</c>: a scope per poll, <b>never throws out of <c>ExecuteAsync</c></b>,
/// disabled in <c>Testing</c> (<c>Reporting:Worker:Enabled=false</c>); tests call <see cref="ReportingWorkCycle"/> directly. A throwing pass is logged and the next one still runs.
/// </summary>
public sealed class ReportingWorker(IServiceScopeFactory scopeFactory, IOptions<ReportingOptions> options, ILogger<ReportingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var worker = options.Value.Worker;
        if (!worker.Enabled)
        {
            logger.LogInformation("ReportingWorker is disabled (Reporting:Worker:Enabled=false).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ReportingWorkCycle>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError("ReportingWorker pass failed with {ExceptionType}; will retry on the next poll.", ex.GetType().Name);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, worker.IntervalSeconds)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }
}
