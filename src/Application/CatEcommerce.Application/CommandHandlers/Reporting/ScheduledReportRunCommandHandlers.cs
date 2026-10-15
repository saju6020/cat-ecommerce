using System.Globalization;
using CatEcommerce.Application.Commands.Reporting;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Reporting;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Reporting.ReportScheduleSupport;

namespace CatEcommerce.Application.CommandHandlers.Reporting;

/// <summary>
/// HO-RPT-001 section 8.9 step 6 / 11.4 C5 (ADR-287, ADR-288) -- records a generated run: <b>one aggregate</b>. Loads the schedule (read) for its recipients, branch and name; creates the run, which
/// references the HO-FIL-001 stored file the runner has already stored and registered as its own step (the same two steps the upload endpoint takes); enlists one email per recipient with the file
/// attached <b>by reference</b> (the run id); and saves the run and the requests in ONE save (HO-NTF-001 F23). If another worker already produced this slot the save is refused by
/// <c>ix_scheduled_report_run_slot</c>: that is <b>success with nothing written</b> -- no second run, no second email -- reported as <c>Created = false</c> so the runner removes the file it stored.
/// No file bytes reach this command, a log or the audit trail.
/// </summary>
public sealed class RecordScheduledReportRunCommandHandler(
    ILogger<RecordScheduledReportRunCommandHandler> logger,
    IAggregateRootRepository<ReportScheduleAggregate> scheduleRepository,
    IAggregateRootRepository<ScheduledReportRunAggregate> runRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IReportCatalogue catalogue,
    IScheduledReportRunStore runStore,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordScheduledReportRunCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordScheduledReportRunCommand command)
    {
        var schedule = await scheduleRepository.GetByIdAsync(command.ScheduleId);
        if (schedule is null)
            return Violation(BusinessViolationCodes.ReportScheduleNotFound, nameof(command.ScheduleId), ReportingMessages.ReportScheduleNotFound);

        var definition = catalogue.Find(schedule.ReportKey);
        if (definition is null)
            return Violation(BusinessViolationCodes.UnknownReport, nameof(schedule.ReportKey), ReportingMessages.UnknownReport);

        // Cheap pre-check; the unique index is the real guard against two workers racing.
        if (command.Trigger == ReportRunTrigger.Schedule)
        {
            var existing = await runRepository.GetByFilterAsync(r => r.ScheduleId == command.ScheduleId && r.DueOn == command.DueOn && r.Trigger == ReportRunTrigger.Schedule
                && (r.Status == ScheduledReportRunStatus.Generated || r.Status == ScheduledReportRunStatus.Purged));
            if (existing is not null) return Success(new RecordedRunResult(existing.Id, existing.Status.DisplayName, existing.RowCount, Created: false));
        }

        var now = timeProvider.GetUtcNow();
        var retentionDays = settings.GetInt(SettingKeys.ReportingExportRetentionDays);

        var run = new ScheduledReportRunAggregate(command.RunId);
        try
        {
            run.RecordGenerated(new ScheduledReportRunDto(
                command.ScheduleId, command.DueOn, command.Trigger, command.PeriodFrom, command.PeriodTo, command.TimeZoneId, command.Format, command.FileName, command.ContentType,
                command.ContentLength, command.ContentSha256, command.RowCount, command.ColumnKeys, command.AttachmentOmitted, command.FileId, now, command.UserContext!));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        try
        {
            await EnqueueDeliveriesAsync(schedule, definition, command, now, retentionDays);
        }
        catch
        {
            // Some requests may already be enlisted: forget them, or the cleanup and Failed-run saves that follow in this scope would persist them.
            runStore.ClearPending();
            throw;
        }

        if (!await runStore.TrySaveAsync(run))
        {
            // Another worker produced this slot first: nothing of ours persisted (the run and its requests were one save).
            logger.LogInformation("Report schedule {ScheduleId} slot {DueOn} was already produced; this run was not kept.", command.ScheduleId, command.DueOn);
            return Success(new RecordedRunResult(command.RunId, ScheduledReportRunStatus.Generated.DisplayName, command.RowCount, Created: false));
        }

        return Success(new RecordedRunResult(run.Id, run.Status.DisplayName, run.RowCount, Created: true));
    }

    private async Task EnqueueDeliveriesAsync(ReportScheduleAggregate schedule, ReportDefinition definition, RecordScheduledReportRunCommand command, DateTimeOffset now, int retentionDays)
    {
        var branch = schedule.BranchId is { } branchId ? await branchRepository.GetByIdAsync(branchId) : null;
        var zone = ResolveZone(command.TimeZoneId);

        var variables = new Dictionary<string, string>
        {
            ["reportTitle"] = definition.Title,
            ["periodLabel"] = PeriodLabel(command.PeriodFrom, command.PeriodTo),
            ["branchName"] = branch?.Name ?? ReportRules.AllBranchesName,
            ["generatedOnLocal"] = TimeZoneInfo.ConvertTime(now, zone).ToString("ddd d MMM yyyy, HH:mm", CultureInfo.InvariantCulture),
            ["rowCount"] = command.RowCount.ToString(CultureInfo.InvariantCulture),
            ["attachmentNote"] = command.AttachmentOmitted ? ReportingMessages.AttachmentTooLargeNote : string.Empty,
            ["reportLink"] = links.ReportLink(definition.Key, command.PeriodFrom, command.PeriodTo, schedule.BranchId),
        };

        // After the file is purged the attachment is gone; the request must not outlive it (a late delivery would otherwise be cancelled by the guard anyway).
        var notAfter = now.AddDays(retentionDays);

        foreach (var recipientId in schedule.RecipientIds)
        {
            outbox.Enqueue(new NotificationRequestDto(
                Guid.NewGuid(), NotificationTemplateCatalogue.ReportScheduledDelivery, NotificationRecipientKind.Staff, recipientId, variables,
                ChannelHint: NotificationChannel.Email, SubjectKind: NotificationSubjectKind.ScheduledReport, SubjectId: command.RunId, SubjectRef: schedule.Id,
                BranchId: schedule.BranchId, DeduplicationKey: $"report-run:{command.RunId:N}:{recipientId:N}", NotAfter: notAfter,
                CorrelationId: command.CorrelationId, UserContext: command.UserContext,
                AttachmentKind: command.AttachmentOmitted ? null : NotificationAttachmentKind.ReportRun, AttachmentId: command.AttachmentOmitted ? null : command.RunId));
        }
    }

    private static string PeriodLabel(DateOnly from, DateOnly to) =>
        from == to
            ? from.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
            : $"{from.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} to {to.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";

    private static TimeZoneInfo ResolveZone(string timeZoneId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (TimeZoneNotFoundException) { return DefaultZone(); }
    }
}

/// <summary>C6 -- a failed run: a <c>Failed</c> row with a failure code, no file and no email. Idempotent per run id.</summary>
public sealed class RecordFailedReportRunCommandHandler(
    IAggregateRootRepository<ReportScheduleAggregate> scheduleRepository,
    IAggregateRootRepository<ScheduledReportRunAggregate> runRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordFailedReportRunCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordFailedReportRunCommand command)
    {
        var schedule = await scheduleRepository.GetByIdAsync(command.ScheduleId);
        if (schedule is null)
            return Violation(BusinessViolationCodes.ReportScheduleNotFound, nameof(command.ScheduleId), ReportingMessages.ReportScheduleNotFound);

        if (await runRepository.GetByIdAsync(command.RunId) is not null) return Success();

        var now = timeProvider.GetUtcNow();
        var zoneId = command.TimeZoneId ?? BillingRules.DefaultTimeZoneId;
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId); }
        catch (TimeZoneNotFoundException) { zone = DefaultZone(); }

        // The branch-local date, not the UTC one (between 18:00 and 24:00 UTC they differ in Dhaka).
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var run = new ScheduledReportRunAggregate(command.RunId);

        try
        {
            run.RecordFailed(
                new ScheduledReportRunDto(
                    command.ScheduleId, command.DueOn, command.Trigger, command.PeriodFrom ?? today, command.PeriodTo ?? today, zoneId,
                    schedule.Format, null, null, 0, null, 0, [], false, null, now, command.UserContext!),
                command.FailureCode);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await runRepository.SaveAsync(run, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
