using System.Text;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 -- US-ADM-06. Audit-log search and CSV export.</summary>
[ApiController]
[Route("api/admin/audit-entries")]
public sealed class AdminAuditEntriesController(
    IInMemoryDispatcher dispatcher,
    IUserContextProvider userContextProvider,
    IClientIpAccessor clientIpAccessor,
    IOptions<AuditExportOptions> auditExportOptions) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.AuditLog.View)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid? actorUserId, [FromQuery] string? entityType, [FromQuery] Guid? entityId,
        [FromQuery] string? action, [FromQuery] string? category, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (from is null || to is null) return BadRequest(new { error = "A date range (from, to) is required." });
        if (to.Value < from.Value) return BadRequest(new { error = "'to' cannot be before 'from'." });
        if ((to.Value - from.Value).TotalDays > AuditExportRules.MaxDateRangeDays)
            return BadRequest(new { error = $"The date range cannot exceed {AuditExportRules.MaxDateRangeDays} days." });
        if (!EnumQueryParameter.TryParse<AuditCategory>(category, out var parsedCategory))
            return BadRequest(new { error = $"'{category}' is not a valid audit category." });
        if (ControlCharacterGuard.ContainsDisallowedControlCharacter(entityType) || ControlCharacterGuard.ContainsDisallowedControlCharacter(action))
            return BadRequest(new { error = "entityType and action must not contain control characters." });
        // Gate fix 2026-09-26: (pageNumber-1)*pageSize can overflow int (Skip overflow); clamp
        // pageNumber to a value that cannot overflow even at the maximum search page size. Shared
        // with AdminLeaveRequestsController via PagingRules (gate fix 2026-09-26, round 4).
        if (pageNumber is > PagingRules.MaxPageNumber) return BadRequest(new { error = $"pageNumber cannot exceed {PagingRules.MaxPageNumber}." });

        var query = new GetAuditEntriesQuery
        {
            ActorUserId = actorUserId, EntityType = entityType, EntityId = entityId, Action = action,
            // Gate fix 2026-09-26: pass the canonical DisplayName on, not the caller's raw casing --
            // the handler's Enumeration.FromDisplayName is case-sensitive.
            Category = parsedCategory?.DisplayName,
            From = from.Value, To = to.Value, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (pageNumber is > 0) query.PageNumber = pageNumber.Value;
        // Gate fix 2026-09-26: clamped -- only the CSV export (below) may ask for AuditExportRules.MaxRows.
        if (pageSize is > 0) query.PageSize = Math.Min(pageSize.Value, AuditExportRules.MaxSearchPageSize);

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("export.csv")]
    [RequireAction(Actions.AuditLog.Export)]
    public async Task<IActionResult> Export(
        [FromQuery] Guid? actorUserId, [FromQuery] string? entityType, [FromQuery] Guid? entityId,
        [FromQuery] string? action, [FromQuery] string? category, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
    {
        if (from is null || to is null) return BadRequest(new { error = "A date range (from, to) is required." });
        if (to.Value < from.Value) return BadRequest(new { error = "'to' cannot be before 'from'." });
        if ((to.Value - from.Value).TotalDays > AuditExportRules.MaxDateRangeDays)
            return BadRequest(new { error = $"The date range cannot exceed {AuditExportRules.MaxDateRangeDays} days." });
        if (!EnumQueryParameter.TryParse<AuditCategory>(category, out var parsedCategory))
            return BadRequest(new { error = $"'{category}' is not a valid audit category." });
        if (ControlCharacterGuard.ContainsDisallowedControlCharacter(entityType) || ControlCharacterGuard.ContainsDisallowedControlCharacter(action))
            return BadRequest(new { error = "entityType and action must not contain control characters." });

        // Gate fix 2026-09-26 (TEST gate I-E3): the cap itself comes from IOptions<AuditExportOptions>
        // (bound from configuration, defaulting to AuditExportRules.MaxRows, 50,000) instead of the
        // constant directly, so a test factory can lower it without generating 50,001 real rows.
        var maxRows = auditExportOptions.Value.MaxRows;

        var userContext = userContextProvider.GetUserContext();
        var query = new GetAuditEntriesQuery
        {
            ActorUserId = actorUserId, EntityType = entityType, EntityId = entityId, Action = action, Category = parsedCategory?.DisplayName,
            From = from.Value, To = to.Value, UserContext = userContext, CorrelationId = Guid.NewGuid(),
            PageNumber = 1, PageSize = maxRows + 1
        };

        var response = await dispatcher.GetResultAsync(query);
        var result = response.Result!;

        if (result.TotalCount > maxRows)
            return StatusCode(413, new { error = AdministrationMessages.AuditExportTooLarge });

        var exportRecord = await dispatcher.SendAsync(new RecordAuditLogExportCommand
        {
            ActorUserId = actorUserId, EntityType = entityType, EntityId = entityId, Action = action, Category = parsedCategory?.DisplayName,
            From = from.Value, To = to.Value, RowCount = result.TotalCount, IpAddress = clientIpAccessor.GetClientIpAddress()?.ToString(),
            UserContext = userContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });
        // Gate fix 2026-09-26: an export whose own audit row failed to write must not still hand
        // back the data -- the write result is checked, not ignored.
        if (!exportRecord.IsSuccess) return StatusCode(500, new { error = "Could not record this export. Try again." });

        // SEC hardening (optional, gate fix 2026-09-26): this is an attachment of audit data, not
        // meant to be cached by an intermediary.
        Response.Headers.CacheControl = "no-store";

        var csv = BuildCsv(result.Items);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "audit-entries.csv");
    }

    /// <summary>ADR-051 -- a cell beginning <c>= + - @</c> (optionally after leading whitespace or
    /// a control character a spreadsheet still treats as leading, gate fix 2026-09-26: tab, CR, LF
    /// or space) is prefixed with <c>'</c> (CSV-injection guard, U-E1).</summary>
    private static string BuildCsv(IReadOnlyList<AuditEntryViewModel> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("OccurredOn,ActorUserId,ActorDisplayName,Action,Category,EntityType,EntityId,Reason,BeforeValue,AfterValue,IpAddress");

        foreach (var item in items)
        {
            sb.AppendLine(string.Join(",",
                Escape(item.OccurredOn.ToString("O")), Escape(item.ActorUserId.ToString()), Escape(item.ActorDisplayName),
                Escape(item.Action), Escape(item.Category), Escape(item.EntityType), Escape(item.EntityId.ToString()),
                Escape(item.Reason), Escape(item.BeforeValue ?? ""), Escape(item.AfterValue ?? ""), Escape(item.IpAddress ?? "")));
        }

        return sb.ToString();
    }

    private static string Escape(string value)
    {
        // Skip leading whitespace/control characters a spreadsheet still honours as "leading"
        // (tab, CR, LF, space) before checking for a formula-triggering character.
        var firstSignificant = value.AsSpan().TrimStart("\t\r\n ");
        var guarded = firstSignificant.Length > 0 && "=+-@".Contains(firstSignificant[0]) ? "'" + value : value;
        var quoted = guarded.Replace("\"", "\"\"");
        return $"\"{quoted}\"";
    }
}
