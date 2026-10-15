using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-BRD-001 section 12.2 (ADR-231, renumbered ADR-364) -- the one asynchronous element Boarding adds: a <see cref="BackgroundService"/> that runs
/// <see cref="BoardingWorkCycle"/> every <c>Boarding:Worker:IntervalSeconds</c> (60). It never throws out of <c>ExecuteAsync</c>, creates a scope per pass, and is
/// disabled by <c>Boarding:Worker:Enabled=false</c> (the Testing configuration).
/// </summary>
public sealed class BoardingWorker(IServiceScopeFactory scopeFactory, IOptions<BoardingOptions> options, ILogger<BoardingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var worker = options.Value.Worker;
        if (!worker.Enabled)
        {
            logger.LogInformation("BoardingWorker is disabled (Boarding:Worker:Enabled=false).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<BoardingWorkCycle>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "BoardingWorker pass failed; will retry on the next poll.");
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
