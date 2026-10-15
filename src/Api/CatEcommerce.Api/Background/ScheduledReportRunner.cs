using System.Security.Cryptography;
using CatEcommerce.Application.Commands.Reporting;
using CatEcommerce.Application.Commands.StoredFile;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Reporting;
using CatEcommerce.Read.Queries.Reporting;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Api.Background;

/// <summary>What one run of a schedule produced. <see cref="Created"/> is false when the slot had already been produced (success, nothing written).</summary>
public sealed record ScheduledRunOutcome(Guid RunId, string Status, int RowCount, string? FailureCode, bool Created);

/// <summary>
/// HO-RPT-001 section 8.9 steps 2 to 7 -- runs ONE schedule: resolve the period as of the due instant in the schedule's zone, work out the columns <b>every</b> recipient may see (the intersection of their
/// restricted actions, ADR-288), dispatch the report in export mode with a <c>ReportAccess</c> built from them (never an unrestricted system identity), render with the same renderers as an interactive
/// export, store the file as its OWN step (<c>IFileStore</c> then HO-FIL-001's <c>RegisterStoredFileCommand</c>, the two steps the upload endpoint takes), then record the run (C5, one aggregate, given
/// the stored file's id) and -- for a scheduled slot -- advance the schedule (C7). A file whose run was not kept (a duplicate slot, a refused or failed save) is deleted in a further, separate step.
/// A failure records a <c>Failed</c> run (C6); a slot with <c>MaxAttemptsPerSlot</c> failed runs, or a PDF that is too large, is advanced anyway so one bad slot never blocks the schedule. A
/// Clinical-category report writes one clinical-access row per recipient first (NFR-SEC-8, fail closed). The worker's pass and the manual run-now endpoint both use it, so the two cannot drift.
/// </summary>
public sealed class ScheduledReportRunner(
    IInMemoryDispatcher dispatcher,
    IReportCatalogue catalogue,
    IEnumerable<IReportExportRenderer> renderers,
    IFileStore fileStore,
    IBusinessSettings settings,
    TimeProvider timeProvider,
    ILogger<ScheduledReportRunner> logger)
{
    public const string UnexpectedFailureCode = "ReportRunFailed";
    public const string ClinicalAccessLogFailedCode = "ClinicalAccessLogFailed";
    private const long BytesPerMegabyte = 1024 * 1024;

    public async Task<ScheduledRunOutcome?> RunAsync(Guid scheduleId, DateTimeOffset dueOn, ReportRunTrigger trigger, UserContext userContext)
    {
        var plan = (await dispatcher.GetResultAsync(new GetReportSchedulePlanQuery { ScheduleId = scheduleId, UserContext = userContext, CorrelationId = Guid.NewGuid() })).Result;
        if (plan is null) return null;

        var definition = catalogue.Find(plan.ReportKey);
        var runId = Guid.NewGuid();
        if (definition is null) return await FailAsync(plan, runId, dueOn, trigger, BusinessViolationCodes.UnknownReport, null, null, userContext);

        var zone = ResolveZone(plan.TimeZoneId);

        // Missed slots collapse to one (ADR-287): after downtime the run is for the LATEST slot that is due, and completing it moves the schedule past now.
        if (trigger == ReportRunTrigger.Schedule) dueOn = LatestDueSlot(plan, dueOn, timeProvider.GetUtcNow(), zone);

        var format = Enumeration.GetAll<ReportFormat>().First(f => f.DisplayName == plan.Format);
        var period = Enumeration.GetAll<ReportRelativePeriod>().First(p => p.DisplayName == plan.Period);
        var asOf = trigger == ReportRunTrigger.Schedule ? dueOn : timeProvider.GetUtcNow();
        var (from, to) = ReportPeriodResolver.Resolve(period, asOf, zone);

        try
        {
            var visible = ReportAccessPolicy.VisibleColumnsForAll(definition, plan.Recipients.Select(r => (IReadOnlyCollection<string>)r.Roles)).Select(c => c.Key).ToList();

            var outcome = (await dispatcher.GetResultAsync(new RunReportQuery
            {
                ReportKey = plan.ReportKey,
                From = from,
                To = to,
                BranchId = plan.BranchId?.ToString() ?? ReportRules.AllBranches,
                ActiveBranchId = plan.BranchId,
                Parameters = plan.Parameters,
                ExportFormat = format,
                CallerRoles = [],
                CallerId = userContext.UserId,
                AccessOverride = new ReportAccess(visible, plan.BranchId, plan.BranchId is null),
                UserContext = userContext,
                CorrelationId = Guid.NewGuid(),
            })).Result!;

            if (outcome.IsRefused) return await FailAsync(plan, runId, dueOn, trigger, outcome.RefusalCode!, from, to, userContext);

            var result = outcome.Result!;
            var renderer = renderers.First(r => r.Format == format);
            var bytes = renderer.Render(result, new ReportRenderContext("PurrCare"));

            // NFR-SEC-8: the clinical text of a Clinical-category report is read by each recipient -- one access row each, before anything is stored or sent. Fail closed.
            if (definition.Category == ReportCategory.Clinical && !await LogClinicalAccessAsync(plan, runId, userContext))
                return await FailAsync(plan, runId, dueOn, trigger, ClinicalAccessLogFailedCode, from, to, userContext);

            var fileName = $"purrcare-{result.ReportKey}-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.{renderer.Extension}";
            var contentType = renderer.ContentType.Split(';')[0].Trim();
            var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var omitted = bytes.LongLength > settings.GetInt(SettingKeys.ReportingMaxAttachmentMegabytes) * BytesPerMegabyte;

            // Step 1: the file, through HO-FIL-001 (its own save). Too large to attach: nothing is stored.
            Guid? fileId = null;
            if (!omitted)
            {
                fileId = await StoreFileAsync(bytes, fileName, contentType, sha256, zone, userContext);
                if (fileId is null) return await FailAsync(plan, runId, dueOn, trigger, BusinessViolationCodes.ReportFileNotStored, from, to, userContext);
            }

            // Step 2: the run (one aggregate). If it is not kept, or the save throws, the file is removed in a separate step; it is never left behind knowingly.
            CommandResponse recorded;
            try
            {
                recorded = await dispatcher.SendAsync(new RecordScheduledReportRunCommand
            {
                RunId = runId,
                ScheduleId = scheduleId,
                DueOn = dueOn,
                Trigger = trigger,
                PeriodFrom = from,
                PeriodTo = to,
                TimeZoneId = zone.Id,
                Format = format,
                // "-to-" between the dates: the name is kept in the file register and the audit trail, and two dates side by side are a 16-digit run that looks like a card number (BIL XI-9).
                FileName = fileName,
                ContentType = contentType,
                ContentLength = bytes.LongLength,
                ContentSha256 = sha256,
                FileId = fileId,
                AttachmentOmitted = omitted,
                RowCount = result.Rows.Count,
                ColumnKeys = result.Columns.Select(c => c.Key).ToList(),
                UserContext = userContext,
                CorrelationId = Guid.NewGuid(),
                ValidateCommand = true
            });
            }
            catch
            {
                await DiscardFileAsync(fileId);
                throw;
            }

            if (!recorded.IsSuccess)
            {
                await DiscardFileAsync(fileId);
                return await FailAsync(plan, runId, dueOn, trigger, recorded.ValidationResult?.Errors.FirstOrDefault()?.ErrorCode ?? UnexpectedFailureCode, from, to, userContext);
            }

            var run = (RecordedRunResult)recorded.Result!;
            if (!run.Created) await DiscardFileAsync(fileId);
            if (trigger == ReportRunTrigger.Schedule) await AdvanceAsync(scheduleId, dueOn, userContext);

            return new ScheduledRunOutcome(run.RunId, run.Status, run.RowCount, null, run.Created);
        }
        catch (ReportTimedOutException)
        {
            return await FailAsync(plan, runId, dueOn, trigger, BusinessViolationCodes.ReportTimedOut, from, to, userContext);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never the exception's message: a failure code only. The type name is enough to find it in the log.
            logger.LogError("Scheduled report run for schedule {ScheduleId} failed with {ExceptionType}.", scheduleId, ex.GetType().Name);
            return await FailAsync(plan, runId, dueOn, trigger, UnexpectedFailureCode, from, to, userContext);
        }
    }

    /// <summary>Stores the bytes and registers the stored file (HO-FIL-001, purpose <c>ReportExport</c>, <c>RetainUntil</c> = the branch-local today + the retention days). Null when either step fails; a stored object whose registration failed is removed.</summary>
    private async Task<Guid?> StoreFileAsync(byte[] bytes, string fileName, string contentType, string sha256, TimeZoneInfo zone, UserContext userContext)
    {
        var now = timeProvider.GetUtcNow();
        var fileId = Guid.NewGuid();

        FileStoreResult<string> put;
        await using (var stream = new MemoryStream(bytes, writable: false))
        {
            put = await fileStore.PutAsync(new FilePutRequest(fileId, FilePurpose.ReportExport, stream, contentType, bytes.LongLength, sha256), CancellationToken.None);
        }

        if (!put.IsSuccess || put.Value is null) return null;

        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        CommandResponse registered;
        try
        {
            registered = await dispatcher.SendAsync(new RegisterStoredFileCommand
        {
            FileId = fileId,
            Purpose = FilePurpose.ReportExport,
            ContentType = contentType,
            SizeInBytes = bytes.LongLength,
            Sha256 = sha256,
            OriginalFileName = fileName,
            StorageKey = put.Value,
            UploadedBy = userContext.UserId,
            UploadedOn = now,
            ScannerName = "ReportingGenerated",
            RetainUntil = localToday.AddDays(settings.GetInt(SettingKeys.ReportingExportRetentionDays)),
            UserContext = userContext,
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        });
        }
        catch
        {
            // A throw leaves the object in the store with no register row, which no retention sweep would ever find: remove it as its own step, then let the run fail.
            await fileStore.DeleteAsync(put.Value, CancellationToken.None);
            throw;
        }

        if (registered.IsSuccess) return fileId;

        await fileStore.DeleteAsync(put.Value, CancellationToken.None);
        return null;
    }

    /// <summary>Removes a stored file whose run was not kept, as its own step. A leftover file carries <c>RetainUntil</c> and is swept by HO-XCT-001's retention sweep; it is never a reason to fail the run.</summary>
    private async Task DiscardFileAsync(Guid? fileId)
    {
        if (fileId is null) return;

        try
        {
            await dispatcher.SendAsync(new DeleteStoredFileCommand
            {
                FileId = fileId.Value, UserContext = new UserContext(ReportingSystemActors.ReportingWorker, Guid.Empty), CorrelationId = Guid.NewGuid()
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning("A report file {FileId} that was not kept could not be deleted ({ExceptionType}); it expires with its retention date.", fileId, ex.GetType().Name);
        }
    }

    /// <summary>NFR-SEC-8 / ADR-129: one clinical-record access row per recipient of a scheduled Clinical-category report. False if any write fails.</summary>
    private async Task<bool> LogClinicalAccessAsync(ReportSchedulePlanViewModel plan, Guid runId, UserContext systemContext)
    {
        foreach (var recipient in plan.Recipients)
        {
            try
            {
                var response = await dispatcher.SendAsync(new RecordClinicalRecordAccessCommand
                {
                    AuditEntryId = Guid.NewGuid(), Kind = ClinicalRecordKind.VisitList, RecordId = CatEcommerce.Application.CommandHandlers.Reporting.RecordReportExportCommandHandler.EntityIdFor(plan.ReportKey), Path = $"scheduled-report/{plan.ReportKey}/{plan.ScheduleId}/{runId}",
                    UserContext = new UserContext(recipient.Id, Guid.Empty), CorrelationId = Guid.NewGuid(), ValidateCommand = true
                });
                if (!response.IsSuccess) return false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError("The clinical-access row for a scheduled report recipient could not be written ({ExceptionType}).", ex.GetType().Name);
                return false;
            }
        }

        return true;
    }

    /// <summary>The latest slot at or before <paramref name="now"/>, walking forward from the stored due time. A schedule that was never behind keeps its stored due time.</summary>
    private static DateTimeOffset LatestDueSlot(ReportSchedulePlanViewModel plan, DateTimeOffset storedDue, DateTimeOffset now, TimeZoneInfo zone)
    {
        var frequency = Enumeration.GetAll<ReportFrequency>().First(f => f.DisplayName == plan.Frequency);
        var latest = storedDue;

        for (var guard = 0; guard < 800; guard++)
        {
            var next = ReportScheduleCalculator.NextAfter(frequency, plan.DayOfWeek, plan.DayOfMonth, plan.LastDayOfMonth, plan.SendAtLocal, latest, zone);
            if (next > now) break;
            latest = next;
        }

        return latest;
    }

    private async Task<ScheduledRunOutcome> FailAsync(
        ReportSchedulePlanViewModel plan, Guid runId, DateTimeOffset dueOn, ReportRunTrigger trigger, string failureCode, DateOnly? from, DateOnly? to, UserContext userContext)
    {
        var recorded = await dispatcher.SendAsync(new RecordFailedReportRunCommand
        {
            RunId = runId, ScheduleId = plan.ScheduleId, DueOn = dueOn, Trigger = trigger, FailureCode = failureCode, PeriodFrom = from, PeriodTo = to, TimeZoneId = plan.TimeZoneId,
            UserContext = userContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!recorded.IsSuccess)
            logger.LogError("The failed run of schedule {ScheduleId} could not be recorded ({FailureCode}).", plan.ScheduleId, failureCode);

        if (trigger == ReportRunTrigger.Schedule)
        {
            var failed = (await dispatcher.GetResultAsync(new CountFailedReportRunsQuery { ScheduleId = plan.ScheduleId, DueOn = dueOn, UserContext = userContext, CorrelationId = Guid.NewGuid() })).Result;
            if (failureCode == BusinessViolationCodes.ReportTooLargeForPdf || failed >= ReportScheduleRules.MaxAttemptsPerSlot)
                await AdvanceAsync(plan.ScheduleId, dueOn, userContext);
        }

        return new ScheduledRunOutcome(runId, ScheduledReportRunStatus.Failed.DisplayName, 0, failureCode, true);
    }

    private async Task AdvanceAsync(Guid scheduleId, DateTimeOffset dueOn, UserContext userContext) =>
        await dispatcher.SendAsync(new AdvanceReportScheduleCommand { ScheduleId = scheduleId, DueOn = dueOn, UserContext = userContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true });

    private static TimeZoneInfo ResolveZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById(BillingRules.DefaultTimeZoneId); }
    }
}
