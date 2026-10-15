using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Retention;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-XCT-001 section 12.2 / ADR-303 -- the third background service. When <c>Operations:RetentionWorker:Enabled</c>, it loops every
/// <c>PollSeconds</c>: the erasure pass every <c>ErasurePassMinutes</c>; the daily passes when the main branch's local hour equals
/// <c>retention.sweepHour</c> and no scheduled run has started on that local date, or when a Manual run is Requested. Each tick is a new scope. It honours
/// the stopping token and never throws out of <c>ExecuteAsync</c>.
/// </summary>
public sealed class RetentionWorker(
    IServiceScopeFactory scopeFactory, IOptions<OperationsOptions> options, ILogger<RetentionWorker> logger, TimeProvider timeProvider)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value.RetentionWorker;
        if (!settings.Enabled)
        {
            logger.LogInformation("RetentionWorker is disabled (Operations:RetentionWorker:Enabled=false).");
            return;
        }

        DateTimeOffset? lastErasurePass = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var cycle = scope.ServiceProvider.GetRequiredService<RetentionWorkCycle>();
                var queue = scope.ServiceProvider.GetRequiredService<RetentionWorkQueue>();
                var clock = scope.ServiceProvider.GetRequiredService<IOperationsClock>();
                var businessSettings = scope.ServiceProvider.GetRequiredService<IBusinessSettings>();

                var now = timeProvider.GetUtcNow();
                if (RetentionSchedule.IsErasurePassDue(now, lastErasurePass))
                {
                    await cycle.RunErasurePassAsync(stoppingToken);
                    lastErasurePass = now;
                }

                if (await queue.GetRequestedRunIdAsync(stoppingToken) is { } requestedRunId)
                {
                    await cycle.RunDailyPassesAsync(RetentionRunTrigger.Manual, requestedRunId, stoppingToken);
                }
                else
                {
                    var time = await clock.NowAsync(stoppingToken);
                    var lastScheduled = await queue.GetLastScheduledRunStartAsync(stoppingToken);
                    if (RetentionSchedule.IsDailyRunDue(time, businessSettings.GetInt(SettingKeys.RetentionSweepHour), lastScheduled))
                        await cycle.RunDailyPassesAsync(RetentionRunTrigger.Scheduled, null, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError("RetentionWorker tick failed ({ExceptionType}); will retry on the next poll.", ex.GetType().Name);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.PollSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }
}
