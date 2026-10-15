using System.Globalization;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Background;
using CatEcommerce.Api.Contracts.Reporting;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Reporting;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Reporting;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-RPT-001 section 12.4 (US-RPT-06, FR-RPT-2) -- report schedules, administrator only (<c>Reports.Schedule</c>). Create and update check the report, parameters, period, branch and every
/// recipient in the handler. <c>run-now</c> is the worker's sequence with <c>Trigger = Manual</c>, completed inside the request (ADR-014); the worker itself runs the due schedules. <c>preview</c> and
/// <c>recipients</c> write nothing -- the client never computes a schedule.
/// </summary>
[ApiController]
[Route("api/admin/report-schedules")]
public sealed class AdminReportSchedulesController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, ScheduledReportRunner runner) : ControllerBase
{
    private static readonly HashSet<string> NotFoundCodes = [BusinessViolationCodes.ReportScheduleNotFound];
    private static readonly HashSet<string> ConflictCodes = [BusinessViolationCodes.StaleVersion];
    private static readonly HashSet<string> ForbiddenCodes = [BusinessViolationCodes.CrossBranchReportNotPermitted];

    private UserContext User => userContextProvider.GetUserContext();

    [HttpGet]
    [RequireAction(Actions.Reports.Schedule)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? reportKey, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null)
    {
        if (!EnumQueryParameter.TryParse<ReportScheduleStatus>(status, out _))
            return BadRequest(Invalid("status", "Choose Active, Paused or Cancelled."));

        var query = new GetReportSchedulesQuery { Status = status, ReportKey = reportKey, UserContext = User, CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("{id:guid}")]
    [RequireAction(Actions.Reports.Schedule)]
    public async Task<IActionResult> Get(Guid id)
    {
        var response = await dispatcher.GetResultAsync(new GetReportScheduleQuery { ScheduleId = id, UserContext = User, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Reports.Schedule)]
    public async Task<IActionResult> Create([FromBody] ReportScheduleRequest request)
    {
        var scheduleId = Guid.NewGuid();
        var (command, failure) = Build(request, update: false, scheduleId);
        if (failure is not null) return failure;

        var response = await dispatcher.SendAsync(command!);
        if (!response.IsSuccess) return Failed(response);

        var result = (ReportScheduleResult)response.Result!;
        return StatusCode(201, new { scheduleId = result.ScheduleId, nextRunOn = result.NextRunOn });
    }

    [HttpPut("{id:guid}")]
    [RequireAction(Actions.Reports.Schedule)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ReportScheduleRequest request)
    {
        if (request.ExpectedVersion is null) return BadRequest(Invalid("expectedVersion", "The version you loaded is required."));

        var (command, failure) = Build(request, update: true, id);
        if (failure is not null) return failure;

        var response = await dispatcher.SendAsync(command!);
        if (!response.IsSuccess) return Failed(response);

        return Ok(new { nextRunOn = ((ReportScheduleResult)response.Result!).NextRunOn });
    }

    [HttpPost("{id:guid}/pause")]
    [RequireAction(Actions.Reports.Schedule)]
    public async Task<IActionResult> Pause(Guid id) =>
        Transition(await dispatcher.SendAsync(new PauseReportScheduleCommand { ScheduleId = id, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true }));

    [HttpPost("{id:guid}/resume")]
    [RequireAction(Actions.Reports.Schedule)]
    public async Task<IActionResult> Resume(Guid id) =>
        Transition(await dispatcher.SendAsync(new ResumeReportScheduleCommand { ScheduleId = id, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true }));

    [HttpPost("{id:guid}/cancel")]
    [RequireAction(Actions.Reports.Schedule)]
    public async Task<IActionResult> Cancel(Guid id) =>
        Transition(await dispatcher.SendAsync(new CancelReportScheduleCommand { ScheduleId = id, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true }));

    /// <summary>The worker's sequence with <c>Trigger = Manual</c>, <c>DueOn = now</c>: not subject to the slot index, does not advance the schedule (section 8.9).</summary>
    [HttpPost("{id:guid}/run-now")]
    [RequireAction(Actions.Reports.Schedule)]
    [EnableRateLimiting(ReportRateLimitPolicies.ReportRunNow)]
    public async Task<IActionResult> RunNow(Guid id, [FromServices] TimeProvider timeProvider)
    {
        var detail = (await dispatcher.GetResultAsync(new GetReportScheduleQuery { ScheduleId = id, UserContext = User, CorrelationId = Guid.NewGuid() })).Result;
        if (detail is null) return NotFound();

        if (detail.Status == ReportScheduleStatus.Cancelled.DisplayName)
            return BadRequest(Invalid("scheduleId", ReportingMessages.ReportScheduleCancelled, BusinessViolationCodes.ReportScheduleCancelled));

        var outcome = await runner.RunAsync(id, timeProvider.GetUtcNow(), ReportRunTrigger.Manual, User);
        if (outcome is null) return NotFound();

        return Ok(new { runId = outcome.RunId, status = outcome.Status, rowCount = outcome.RowCount, failureCode = outcome.FailureCode });
    }

    [HttpPost("preview")]
    [RequireAction(Actions.Reports.Schedule)]
    public async Task<IActionResult> Preview([FromBody] ReportSchedulePreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReportKey)) return BadRequest(Invalid("reportKey", "Choose a report."));
        if (!TryParseCommon(request.Period, request.Frequency, request.DayOfWeek, request.SendAtLocal, out var period, out var frequency, out var dayOfWeek, out var sendAt, out var failure))
            return BadRequest(failure);

        var response = await dispatcher.GetResultAsync(new PreviewReportScheduleQuery
        {
            ReportKey = request.ReportKey, BranchId = request.BranchId, Period = period!, Frequency = frequency!, DayOfWeek = dayOfWeek, DayOfMonth = request.DayOfMonth,
            LastDayOfMonth = request.LastDayOfMonth, SendAtLocal = sendAt, UserContext = User, CorrelationId = Guid.NewGuid()
        });

        var outcome = response.Result!;
        if (!outcome.IsRefused) return Ok(outcome.Result);

        var body = Invalid(outcome.RefusalProperty ?? "schedule", outcome.RefusalMessage ?? "", outcome.RefusalCode);
        return outcome.RefusalCode == BusinessViolationCodes.UnknownReport ? NotFound(body) : BadRequest(body);
    }

    [HttpGet("{id:guid}/runs")]
    [RequireAction(Actions.Reports.Schedule)]
    public async Task<IActionResult> Runs(Guid id, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null)
    {
        var query = new GetReportScheduleRunsQuery { ScheduleId = id, UserContext = User, CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("recipients")]
    [RequireAction(Actions.Reports.Schedule)]
    public async Task<IActionResult> Recipients([FromQuery] string reportKey, [FromQuery] Guid? branchId)
    {
        var response = await dispatcher.GetResultAsync(new GetEligibleReportRecipientsQuery { ReportKey = reportKey, BranchId = branchId, UserContext = User, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound(Invalid("reportKey", ReportingMessages.UnknownReport, BusinessViolationCodes.UnknownReport)) : Ok(response.Result);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────

    private (ReportScheduleCommandBase? Command, IActionResult? Failure) Build(ReportScheduleRequest request, bool update, Guid scheduleId)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return (null, BadRequest(Invalid("name", "A schedule needs a name.")));
        if (string.IsNullOrWhiteSpace(request.ReportKey)) return (null, BadRequest(Invalid("reportKey", "Choose a report.")));
        if (request.RecipientIds is null) return (null, BadRequest(Invalid("recipientIds", ReportingMessages.ReportRecipientRequired, BusinessViolationCodes.ReportRecipientRequired)));
        if (!TryParseCommon(request.Period, request.Frequency, request.DayOfWeek, request.SendAtLocal, out var period, out var frequency, out var dayOfWeek, out var sendAt, out var failure))
            return (null, BadRequest(failure));
        if (!EnumerationParsing.TryParse<ReportFormat>(request.Format, out var format) || format is null)
            return (null, BadRequest(Invalid("format", ReportingMessages.ReportFormatNotSupported, BusinessViolationCodes.ReportFormatNotSupported)));

        var parameters = request.Parameters ?? new Dictionary<string, string>();

        ReportScheduleCommandBase command = update
            ? new UpdateReportScheduleCommand
            {
                ScheduleId = scheduleId, Name = request.Name, ReportKey = request.ReportKey, BranchId = request.BranchId, Period = period!, Parameters = parameters, Format = format,
                Frequency = frequency!, DayOfWeek = dayOfWeek, DayOfMonth = request.DayOfMonth, LastDayOfMonth = request.LastDayOfMonth, SendAtLocal = sendAt, RecipientIds = request.RecipientIds,
                ExpectedVersion = request.ExpectedVersion!.Value, UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }
            : new CreateReportScheduleCommand
            {
                ScheduleId = scheduleId, Name = request.Name, ReportKey = request.ReportKey, BranchId = request.BranchId, Period = period!, Parameters = parameters, Format = format,
                Frequency = frequency!, DayOfWeek = dayOfWeek, DayOfMonth = request.DayOfMonth, LastDayOfMonth = request.LastDayOfMonth, SendAtLocal = sendAt, RecipientIds = request.RecipientIds,
                UserContext = User, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            };

        return (command, null);
    }

    private static bool TryParseCommon(
        string? periodText, string? frequencyText, string? dayOfWeekText, string? sendAtText,
        out ReportRelativePeriod? period, out ReportFrequency? frequency, out DayOfWeek? dayOfWeek, out TimeOnly sendAt, out object? failure)
    {
        period = null;
        frequency = null;
        dayOfWeek = null;
        sendAt = default;
        failure = null;

        if (!EnumerationParsing.TryParse(periodText, out period) || period is null) { failure = Invalid("period", "Choose one of the listed periods."); return false; }
        if (!EnumerationParsing.TryParse(frequencyText, out frequency) || frequency is null) { failure = Invalid("frequency", "Choose daily, weekly or monthly."); return false; }
        if (!TimeOnly.TryParseExact(sendAtText, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out sendAt)) { failure = Invalid("sendAtLocal", "Give the send time as HH:mm."); return false; }

        if (!string.IsNullOrWhiteSpace(dayOfWeekText))
        {
            if (!Enum.TryParse<DayOfWeek>(dayOfWeekText, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed)) { failure = Invalid("dayOfWeek", "Choose a day of the week."); return false; }
            dayOfWeek = parsed;
        }

        return true;
    }

    private IActionResult Transition(CommandResponse response) => response.IsSuccess ? NoContent() : Failed(response);

    private IActionResult Failed(CommandResponse response)
    {
        var codes = response.ValidationResult?.Errors.Select(e => e.ErrorCode).ToHashSet(StringComparer.Ordinal) ?? [];
        object body = (object?)response.ValidationResult ?? new { error = "The request could not be completed." };

        if (codes.Overlaps(NotFoundCodes)) return NotFound(body);
        if (codes.Overlaps(ConflictCodes)) return Conflict(body);
        return codes.Overlaps(ForbiddenCodes) ? StatusCode(403, body) : BadRequest(body);
    }

    private static object Invalid(string property, string message, string code = BusinessViolationCodes.InvalidReportSchedule) =>
        new { error = code, errors = new[] { new { errorCode = code, errorMessage = message, propertyName = property } } };
}
