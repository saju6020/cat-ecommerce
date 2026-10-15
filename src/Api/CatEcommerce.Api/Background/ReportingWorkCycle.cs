using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Read.Queries.Reporting;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-RPT-001 section 8.9 / 12.2 (ADR-287, ADR-290) -- one pass of the reporting worker: the due schedules, <b>sequentially</b> (one report at a time, so reports can never starve interactive work), each in
/// its own scope so a failing schedule is logged and never stops the rest. The purge of expired report files (section 8.10, C8) is NOT in this pass yet: it needs HO-XCT-001's retention sweep, which
/// is not merged (see HO-RPT-001 section 1 Amended). Idempotent; Scoped; callable by tests.
/// </summary>
public sealed class ReportingWorkCycle(ILogger<ReportingWorkCycle> logger, IServiceScopeFactory scopeFactory, IInMemoryDispatcher dispatcher, TimeProvider timeProvider)
{
    private static UserContext WorkerContext => new(ReportingSystemActors.ReportingWorker, Guid.Empty) { UserName = "reporting-worker" };

    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            await RunDueSchedulesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError("The due-schedules pass failed with {ExceptionType}; the next pass will retry.", ex.GetType().Name);
        }
    }

    public async Task RunDueSchedulesAsync(CancellationToken ct = default)
    {
        var due = (await dispatcher.GetResultAsync(new GetDueReportSchedulesQuery
        {
            Now = timeProvider.GetUtcNow(), Limit = ReportScheduleRules.MaxSchedulesPerPass, UserContext = WorkerContext, CorrelationId = Guid.NewGuid()
        })).Result ?? [];

        foreach (var schedule in due)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<ScheduledReportRunner>();
                await runner.RunAsync(schedule.ScheduleId, schedule.DueOn, ReportRunTrigger.Schedule, WorkerContext);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError("Schedule {ScheduleId} failed with {ExceptionType}; the other schedules still run.", schedule.ScheduleId, ex.GetType().Name);
            }
        }
    }
}
