using System.Globalization;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.CommandHandlers.Reporting;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.Commands.Reporting;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Infrastructure.Reporting;
using CatEcommerce.Read.Queries.Reporting;
using CatEcommerce.Read.QueryHandlers.Reporting;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-RPT-001 section 12.4 (US-RPT-01, FR-RPT-1) -- the catalogue, one generic query endpoint, and the CSV and PDF exports of every report. Every endpoint carries the <c>Reports.Any</c> marker; the
/// report's own category action is then enforced in the query handler by <c>ReportAccessPolicy</c>, and a denial there answers the <b>same</b> <c>403 ActionNotPermitted</c> body as the fail-closed
/// filter and is audited the same way (section 12.3). <c>404 UnknownReport</c> is only for a key that is in no catalogue. View and export use the <c>reports</c> concurrency policy (ADR-290).
/// Viewing is not audited (a query never writes); every export is, before the first byte is streamed (ADR-285).
/// </summary>
[ApiController]
[Route("api/staff/reports")]
public sealed class StaffReportsController(
    IInMemoryDispatcher dispatcher,
    IUserContextProvider userContextProvider,
    IActiveBranchAccessor activeBranchAccessor,
    IClientIpAccessor clientIpAccessor,
    IEnumerable<IReportExportRenderer> renderers,
    IReportCatalogue catalogue,
    ILogger<StaffReportsController> logger) : ControllerBase
{
    private static readonly string[] ReservedQueryNames = ["from", "to", "branchId", "groupBy", "sort", "pageNumber", "pageSize"];

    [HttpGet]
    [RequireAction(Actions.Reports.Any)]
    public async Task<IActionResult> Catalogue()
    {
        var active = activeBranchAccessor.GetActiveBranchId();
        var response = await dispatcher.GetResultAsync(new GetReportCatalogueQuery
        {
            CallerRoles = CallerRoles(), ActiveBranchId = active.BranchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        Response.Headers.CacheControl = "no-store";
        return Ok(response.Result);
    }

    [HttpGet("{reportKey}")]
    [RequireAction(Actions.Reports.Any)]
    [EnableRateLimiting(ReportRateLimitPolicies.Reports)]
    public async Task<IActionResult> Run(string reportKey, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = ReportRules.DefaultPageSize)
    {
        var outcome = await RunAsync(reportKey, exportFormat: null, pageNumber, pageSize);
        if (outcome.IsRefused) return await RefusalAsync(outcome);
        if (!await TryLogClinicalAccessAsync(outcome.Result!)) return ClinicalAccessFailed();

        Response.Headers.CacheControl = "no-store";
        return Ok(ReportResultFormatter.ToJson(outcome.Result!));
    }

    [HttpGet("{reportKey}/export.csv")]
    [RequireAction(Actions.Reports.Any)]
    [EnableRateLimiting(ReportRateLimitPolicies.Reports)]
    public Task<IActionResult> ExportCsv(string reportKey) => ExportAsync(reportKey, ReportFormat.Csv);

    [HttpGet("{reportKey}/export.pdf")]
    [RequireAction(Actions.Reports.Any)]
    [EnableRateLimiting(ReportRateLimitPolicies.Reports)]
    public Task<IActionResult> ExportPdf(string reportKey) => ExportAsync(reportKey, ReportFormat.Pdf);

    private async Task<IActionResult> ExportAsync(string reportKey, ReportFormat format)
    {
        var outcome = await RunAsync(reportKey, format, pageNumber: 1, pageSize: ReportRules.DefaultPageSize);
        if (outcome.IsRefused) return await RefusalAsync(outcome);

        var result = outcome.Result!;
        if (!await TryLogClinicalAccessAsync(result)) return ClinicalAccessFailed();
        var parameters = QueryParameters();

        // The audit comes BEFORE the first byte is streamed; a failed audit write fails the export (an unaudited financial export is worse than a retry).
        try
        {
            var audit = await dispatcher.SendAsync(new RecordReportExportCommand
            {
                ReportKey = result.ReportKey, Format = format, From = result.Filters.From, To = result.Filters.To, BranchId = Request.Query["branchId"].FirstOrDefault(),
                Parameters = parameters, RowCount = result.Rows.Count, ColumnKeys = result.Columns.Select(c => c.Key).ToList(),
                IpAddress = clientIpAccessor.GetClientIpAddress()?.ToString(), UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
            });

            if (!audit.IsSuccess) return AuditFailed();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The export audit for report {ReportKey} could not be written; the export was not released.", result.ReportKey);
            return AuditFailed();
        }

        var renderer = renderers.First(r => r.Format == format);
        var bytes = renderer.Render(result, new ReportRenderContext("PurrCare"));

        Response.Headers.CacheControl = "no-store";
        var fileName = $"purrcare-{result.ReportKey}-{result.Filters.From:yyyy-MM-dd}-to-{result.Filters.To:yyyy-MM-dd}.{renderer.Extension}";
        return File(bytes, renderer.ContentType, fileName);
    }

    private async Task<ReportOutcome> RunAsync(string reportKey, ReportFormat? exportFormat, int pageNumber, int pageSize)
    {
        var request = Request.Query;
        var active = activeBranchAccessor.GetActiveBranchId();
        var requestedBranch = request["branchId"].FirstOrDefault();

        var response = await dispatcher.GetResultAsync(new RunReportQuery
        {
            ReportKey = reportKey,
            From = ParseDate(request["from"].FirstOrDefault()),
            To = ParseDate(request["to"].FirstOrDefault()),
            BranchId = requestedBranch,
            ActiveBranchId = active.BranchId,
            GroupBy = request["groupBy"].FirstOrDefault(),
            Sort = request["sort"].FirstOrDefault(),
            Parameters = QueryParameters(),
            ExportFormat = exportFormat,
            CallerRoles = CallerRoles(),
            CallerId = Caller()?.AccountId ?? Guid.Empty,
            PageNumber = pageNumber,
            PageSize = pageSize,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
        });

        return response.Result!;
    }

    private async Task<IActionResult> RefusalAsync(ReportOutcome outcome)
    {
        var code = outcome.RefusalCode!;
        var body = new
        {
            error = code,
            errors = new[] { new { errorCode = code, errorMessage = outcome.RefusalMessage, propertyName = outcome.RefusalProperty } }
        };

        Response.Headers.CacheControl = "no-store";

        if (code == BusinessViolationCodes.UnknownReport) return NotFound(body);

        if (code == BusinessViolationCodes.ActionNotPermitted)
        {
            await RecordDenialAsync(outcome.RefusalProperty);
            return StatusCode(403, new { errors = new[] { new { errorCode = code, errorMessage = ReportingMessages.ActionNotPermitted } } });
        }

        if (code is BusinessViolationCodes.BranchNotAssigned or BusinessViolationCodes.CrossBranchReportNotPermitted) return StatusCode(403, body);
        if (code is BusinessViolationCodes.ReportTooLargeForCsv or BusinessViolationCodes.ReportTooLargeForPdf) return StatusCode(413, body);

        return BadRequest(body);
    }

    /// <summary>
    /// NFR-SEC-8 / ADR-129 (owner decision 2026-10-13): a Clinical-category report carries clinical text (R3's complications), so every view and every CSV or PDF export writes a clinical-access row,
    /// the same mechanism as HO-SRG-001's surgical-report read. Fail closed: <c>false</c> means nothing is released and the caller answers a bare 500.
    /// </summary>
    private async Task<bool> TryLogClinicalAccessAsync(ReportResult result)
    {
        if (catalogue.Find(result.ReportKey)?.Category != ReportCategory.Clinical) return true;

        try
        {
            var response = await dispatcher.SendAsync(new RecordClinicalRecordAccessCommand
            {
                AuditEntryId = Guid.NewGuid(), Kind = ClinicalRecordKind.VisitList, RecordId = RecordReportExportCommandHandler.EntityIdFor(result.ReportKey), Path = Request.Path.Value ?? "",
                IpAddress = clientIpAccessor.GetClientIpAddress()?.ToString(), UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
            });
            return response.IsSuccess;
        }
        catch (Exception ex)
        {
            logger.LogError("The clinical-access row for report {ReportKey} could not be written ({ExceptionType}); the report was not released.", result.ReportKey, ex.GetType().Name);
            return false;
        }
    }

    private ObjectResult ClinicalAccessFailed() => StatusCode(500, new { error = "The request could not be completed." });

    /// <summary>The in-handler half of the access check audits its denial exactly as the fail-closed filter does (HO-ACC-001 C11).</summary>
    private async Task RecordDenialAsync(string? requiredAction)
    {
        try
        {
            await dispatcher.SendAsync(new RecordAuthorizationDenialCommand
            {
                Action = requiredAction ?? Actions.Reports.Any, Method = Request.Method, Path = Request.Path.Value ?? "",
                IpAddress = clientIpAccessor.GetClientIpAddress()?.ToString(), UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
            });
        }
        catch (Exception ex)
        {
            // Never an allow, never a 500: the denial itself is still returned.
            logger.LogError(ex, "Failed to record the authorization denial for a report.");
        }
    }

    private ObjectResult AuditFailed() =>
        StatusCode(500, new { errors = new[] { new { errorCode = "ExportAuditFailed", errorMessage = "The export could not be recorded, so it was not released. Try again." } } });

    private Dictionary<string, string> QueryParameters() =>
        Request.Query.Where(q => !ReservedQueryNames.Contains(q.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase);

    private static DateOnly? ParseDate(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private ResolvedCaller? Caller() => HttpContext.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller;

    private IReadOnlyList<string> CallerRoles() => Caller() is { Kind: ResolvedCallerKind.Staff } caller ? caller.RoleIds : [];
}
