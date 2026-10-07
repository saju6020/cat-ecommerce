using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-NTF-001 §12.2 / ADR-060/ADR-065 -- the only asynchronous element in the solution. When
/// <c>Worker:Enabled</c>, loops every <c>PollIntervalSeconds</c> calling
/// <see cref="NotificationWorkCycle.RunDeliveryPassAsync"/>, and -- once 30 s after start, then
/// every <c>ReminderSweepIntervalMinutes</c> -- calls <c>RunReminderSweepAsync</c>, each in a new
/// scope. Never throws out of <c>ExecuteAsync</c>; honours the stopping token.
/// </summary>
public sealed class NotificationWorker(
    IServiceScopeFactory scopeFactory, IOptions<NotificationOptions> options, ILogger<NotificationWorker> logger, TimeProvider timeProvider)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var worker = options.Value.Worker;
        if (!worker.Enabled)
        {
            logger.LogInformation("NotificationWorker is disabled (Notifications:Worker:Enabled=false).");
            return;
        }

        LogUnconfiguredSendersOnce();

        // Gate fix 2026-09-27 (CODE_REVIEW LOW): TimeProvider, not DateTimeOffset.UtcNow -- matches
        // every other instant in this slice, and can be driven by FakeTimeProvider in a test.
        var nextSweep = timeProvider.GetUtcNow().AddSeconds(30);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var workCycle = scope.ServiceProvider.GetRequiredService<NotificationWorkCycle>();
                await workCycle.RunDeliveryPassAsync(stoppingToken);

                if (timeProvider.GetUtcNow() >= nextSweep)
                {
                    await workCycle.RunReminderSweepAsync(stoppingToken);
                    nextSweep = timeProvider.GetUtcNow().AddMinutes(worker.ReminderSweepIntervalMinutes);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "NotificationWorker pass failed; will retry on the next poll.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(worker.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }

    /// <summary>§12.2 -- a one-time, loud warning at start if either channel has no real provider
    /// configured outside Development (ADR-061).</summary>
    private void LogUnconfiguredSendersOnce()
    {
        if (options.Value.Channels.Sms.Sender == "Unconfigured")
            logger.LogWarning(NotificationMessages.StartupNoSmsProviderWarning);

        if (options.Value.Channels.Email.Sender == "Unconfigured")
            logger.LogWarning(NotificationMessages.StartupNoEmailProviderWarning);
    }
}
