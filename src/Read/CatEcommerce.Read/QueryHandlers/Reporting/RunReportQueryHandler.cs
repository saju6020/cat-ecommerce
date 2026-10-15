using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Infrastructure.Reporting;
using CatEcommerce.Read.Queries.Reporting;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Reporting;

/// <summary>
/// HO-RPT-001 section 12.6 / ADR-280 -- the one pipeline behind every report. Date range, branch, restricted columns, sort, paging and the export caps are applied here, so no report can be
/// missing them (FR-RPT-1). The report's own category action is enforced here with <see cref="ReportAccessPolicy"/> (the in-handler half of the two-layer check, section 12.3).
/// Everything below runs inside <see cref="IReportingReadScope"/>: a READ ONLY transaction that is always rolled back (ADR-279).
/// </summary>
public sealed class RunReportQueryHandler(
    IReportCatalogue catalogue,
    IEnumerable<IReportSource> sources,
    IReportingReadScope scope,
    IOptions<ReportingOptions> options,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : IQueryHandlerAsync<RunReportQuery, ReportOutcome>
{
    public async Task<QueryResponse<ReportOutcome>> HandleAsync(RunReportQuery query)
    {
        var outcome = await RunAsync(query);
        return new QueryResponse<ReportOutcome> { Result = outcome };
    }

    private async Task<ReportOutcome> RunAsync(RunReportQuery query)
    {
        var definition = catalogue.Find(query.ReportKey);
        var source = definition is null ? null : sources.FirstOrDefault(s => string.Equals(s.Key, definition.Key, StringComparison.Ordinal));

        // A key that is in no catalogue -- or whose source is not registered -- is the only 404.
        if (definition is null || source is null)
            return ReportOutcome.Refused(BusinessViolationCodes.UnknownReport, ReportingMessages.UnknownReport, nameof(query.ReportKey));

        if (query.AccessOverride is null && !ReportAccessPolicy.CanOpen(definition, query.CallerRoles))
            return ReportOutcome.Refused(BusinessViolationCodes.ActionNotPermitted, ReportingMessages.ActionNotPermitted, ReportRules.ActionFor(definition.Category));

        return await scope.RunAsync((db, ct) => RunInScopeAsync(query, definition, source, db, ct), CancellationToken.None);
    }

    private async Task<ReportOutcome> RunInScopeAsync(RunReportQuery query, ReportDefinition definition, IReportSource source, CatEcommerceDbContext db, CancellationToken ct)
    {
        var reporting = options.Value;

        // ── Branch scope (section 8.7) ──────────────────────────────────────────────────────────────
        ReportBranchScope branchScope;
        if (query.AccessOverride is { } access)
        {
            branchScope = access.AllBranches || access.BranchId is null ? ReportBranchScope.All : ReportBranchScope.Single(access.BranchId.Value);
        }
        else
        {
            var assigned = await db.Set<RvStaffBranchRow>().AsNoTracking().Where(a => a.StaffAccountId == query.CallerId).Select(a => a.BranchId).ToListAsync(ct);
            branchScope = ReportAccessPolicy.ResolveBranch(query.BranchId, query.ActiveBranchId, assigned, query.CallerRoles.ToList());
        }

        if (branchScope.IsRefused)
            return ReportOutcome.Refused(branchScope.RefusalCode!, branchScope.RefusalMessage!, "branchId");

        var branches = db.Set<RvBranchRow>().AsNoTracking();
        var reportBranch = branchScope.BranchId is { } id
            ? await branches.Where(b => b.BranchId == id).FirstOrDefaultAsync(ct)
            : await branches.Where(b => b.BranchId == AdministrationSeed.MainBranchId).FirstOrDefaultAsync(ct);

        if (branchScope.BranchId is not null && reportBranch is null)
            return ReportOutcome.Refused(BusinessViolationCodes.BranchNotAssigned, ReportingMessages.BranchNotAssigned, "branchId");

        var zone = ResolveZone(reportBranch?.TimeZoneId);

        // ── Filters (FR-RPT-1) ──────────────────────────────────────────────────────────────────────
        var (filters, violation) = ReportFilterValidator.Validate(definition, query.From, query.To, query.GroupBy, query.Parameters);
        if (violation is not null) return ReportOutcome.Refused(violation.Code, violation.Message, violation.Property);

        var visible = query.AccessOverride is { } overridden
            ? definition.Columns.Where(c => overridden.VisibleColumnKeys.Contains(c.Key)).ToList()
            : ReportAccessPolicy.VisibleColumns(definition, query.CallerRoles.ToList());

        var (sortKey, sortDescending, sortViolation) = ResolveSort(query.Sort ?? definition.DefaultSort, visible);
        if (sortViolation is not null) return ReportOutcome.Refused(BusinessViolationCodes.InvalidReportParameter, ReportingMessages.InvalidReportParameter("sort"), "sort");

        // ── Row limit: the format cap + 1 in export mode, so the handler can refuse without loading everything ──
        var exporting = query.ExportFormat is not null;
        var cap = query.ExportFormat == ReportFormat.Pdf ? reporting.PdfMaxRows : reporting.CsvMaxRows;
        var rowLimit = exporting ? cap + 1 : reporting.CsvMaxRows + 1;

        var (start, end) = ReportPeriodResolver.ToInstants(filters!.From, filters.To, zone);
        var now = query.AsOf ?? timeProvider.GetUtcNow();
        var request = new ReportRequest(
            definition, filters.From, filters.To, start, end, branchScope.BranchId, branchScope.IsAllBranches, zone, filters.GroupBy, filters.Parameters,
            visible.Select(c => c.Key).ToHashSet(StringComparer.Ordinal), rowLimit, now,
            settings.GetString(SettingKeys.CommerceCurrencyCode, branchScope.BranchId), settings);

        var produced = await source.RunAsync(request, db, ct);

        // ── Caps ────────────────────────────────────────────────────────────────────────────────────
        var notes = (produced.Notes ?? []).ToList();
        var rows = produced.Rows.ToList();

        if (exporting && rows.Count > cap)
        {
            return query.ExportFormat == ReportFormat.Pdf
                ? ReportOutcome.Refused(BusinessViolationCodes.ReportTooLargeForPdf, ReportingMessages.ReportTooLargeForPdf)
                : ReportOutcome.Refused(BusinessViolationCodes.ReportTooLargeForCsv, ReportingMessages.ReportTooLargeForCsv);
        }

        if (!exporting && rows.Count > reporting.CsvMaxRows)
        {
            rows = rows.Take(reporting.CsvMaxRows).ToList();
            notes.Add(new ReportNote("RowCeilingReached", ReportingMessages.RowCeilingReached));
        }

        // ── Restricted columns: removed from rows, totals and coverage, not blanked ────────────────
        var visibleKeys = request.VisibleColumnKeys;
        rows = rows.Select(r => Restrict(r, visibleKeys)).ToList();
        var totals = produced.Totals is null ? null : Restrict(produced.Totals, visibleKeys);
        var coverage = produced.Coverage;

        // ── Sort and page ───────────────────────────────────────────────────────────────────────────
        if (sortKey is not null) rows = Sort(rows, sortKey, sortDescending).ToList();

        var pageSize = Math.Clamp(query.PageSize <= 0 ? ReportRules.DefaultPageSize : query.PageSize, 1, ReportRules.MaxPageSize);
        var pageNumber = Math.Max(query.PageNumber, 1);
        var total = rows.Count;
        IReadOnlyList<ReportRow> page = exporting ? rows : rows.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        var echo = new ReportFilters(
            filters.From, filters.To, branchScope.BranchId, branchScope.IsAllBranches ? ReportRules.AllBranchesName : reportBranch!.Name, zone.Id,
            filters.GroupBy?.DisplayName, filters.Parameters);

        if (branchScope.IsAllBranches && reportBranch is not null)
            notes.Add(new ReportNote("AllBranchesZone", $"The date range is read in the main branch's time zone ({zone.Id})."));

        var generatedBy = query.AccessOverride is not null
            ? "Scheduled report"
            : await db.Set<RvStaffRow>().AsNoTracking().Where(s => s.StaffAccountId == query.CallerId).Select(s => s.FullName).FirstOrDefaultAsync(ct) ?? "Staff";

        return ReportOutcome.Success(new ReportResult(
            definition.Key, definition.Title, now, generatedBy, echo, visible, page, definition.HasTotals ? totals : null, notes, coverage,
            exporting ? 1 : pageNumber, exporting ? total : pageSize, total));
    }

    private static TimeZoneInfo ResolveZone(string? timeZoneId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? BillingRules.DefaultTimeZoneId : timeZoneId); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById(BillingRules.DefaultTimeZoneId); }
    }

    private static ReportRow Restrict(ReportRow row, IReadOnlyCollection<string> visibleKeys)
    {
        var kept = new ReportRow();
        foreach (var (key, value) in row)
            if (visibleKeys.Contains(key)) kept[key] = value;
        return kept;
    }

    private static (string? Key, bool Descending, string? Violation) ResolveSort(string? sort, IReadOnlyList<ReportColumn> visible)
    {
        if (string.IsNullOrWhiteSpace(sort)) return (null, false, null);

        var descending = sort.StartsWith('-');
        var key = descending ? sort[1..] : sort;
        var column = visible.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));
        return column is { Sortable: true } ? (column.Key, descending, null) : (null, false, "sort");
    }

    private static IEnumerable<ReportRow> Sort(List<ReportRow> rows, string key, bool descending)
    {
        var comparer = Comparer<object?>.Create((a, b) => Compare(a, b));
        var keyed = rows.Select(row => (row, value: row.GetValueOrDefault(key)));

        // Nulls last in both directions; the original position breaks ties so the sort is stable.
        var withValue = keyed.Where(x => x.value is not null);
        var nulls = keyed.Where(x => x.value is null).Select(x => x.row);
        var ordered = descending ? withValue.OrderByDescending(x => x.value, comparer) : withValue.OrderBy(x => x.value, comparer);
        return ordered.Select(x => x.row).Concat(nulls);
    }

    private static int Compare(object? a, object? b)
    {
        if (a is ReportMoney ma && b is ReportMoney mb) return ma.Amount.CompareTo(mb.Amount);
        if (a is string sa && b is string sb) return string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);
        if (a is DateOnly da && b is DateOnly db) return da.CompareTo(db);
        if (a is DateTimeOffset ta && b is DateTimeOffset tb) return ta.CompareTo(tb);
        if (a is bool ba && b is bool bb) return ba.CompareTo(bb);
        if (a is IConvertible ca && b is IConvertible cb)
            return Convert.ToDecimal(ca, System.Globalization.CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(cb, System.Globalization.CultureInfo.InvariantCulture));
        return string.Compare(a?.ToString(), b?.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
