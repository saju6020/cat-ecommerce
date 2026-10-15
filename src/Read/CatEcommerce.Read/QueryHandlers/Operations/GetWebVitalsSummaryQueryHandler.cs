using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Operations;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 12.6 Q2, ADR-310. One SQL statement with <c>percentile_cont(0.75)</c>, grouped by route, viewport and metric over the budgeted
/// metrics (LCP, INP, CLS); the verdict is decided here from <see cref="WebVitalBudgets"/>. FCP and TTFB are collected but have no budget, so they are not judged.
/// </summary>
public sealed class GetWebVitalsSummaryQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetWebVitalsSummaryQuery, WebVitalsSummaryViewModel>
{
    public sealed class SummaryRow
    {
        public string RouteTemplate { get; set; } = default!;
        public int ViewportClass { get; set; }
        public int Metric { get; set; }
        public int SampleCount { get; set; }
        public decimal P75 { get; set; }
    }

    public async Task<QueryResponse<WebVitalsSummaryViewModel>> HandleAsync(GetWebVitalsSummaryQuery query)
    {
        var parameters = new List<NpgsqlParameter>
        {
            new("from", NpgsqlDbType.TimestampTz) { Value = query.From },
            new("to", NpgsqlDbType.TimestampTz) { Value = query.To },
            new("lcp", WebVitalMetric.LCP.Value), new("inp", WebVitalMetric.INP.Value), new("cls", WebVitalMetric.CLS.Value),
        };

        var filter = string.Empty;
        if (!string.IsNullOrWhiteSpace(query.RouteTemplate))
        {
            filter += " AND s.route_template = @route";
            parameters.Add(new NpgsqlParameter("route", NpgsqlDbType.Text) { Value = query.RouteTemplate });
        }
        if (!string.IsNullOrWhiteSpace(query.ViewportClass))
        {
            filter += " AND s.viewport_class = @viewport";
            parameters.Add(new NpgsqlParameter("viewport", Enumeration.FromDisplayName<ViewportClass>(query.ViewportClass).Value));
        }

        var sql = $"""
            SELECT s.route_template AS "RouteTemplate", s.viewport_class AS "ViewportClass", s.metric AS "Metric",
                   COUNT(*)::int AS "SampleCount",
                   (percentile_cont(0.75) WITHIN GROUP (ORDER BY s.value))::numeric AS "P75"
            FROM ops.web_vital_sample s
            JOIN ops.web_vital_batch b ON b."Id" = s.web_vital_batch_id
            WHERE b.received_on >= @from AND b.received_on < @to AND s.metric IN (@lcp, @inp, @cls){filter}
            GROUP BY s.route_template, s.viewport_class, s.metric
            ORDER BY s.route_template, s.viewport_class, s.metric
            """;

        var rows = await db.Database.SqlQueryRaw<SummaryRow>(sql, parameters.ToArray<object>()).ToListAsync();

        var items = rows.Select(r =>
        {
            var metric = Enumeration.FromValue<WebVitalMetric>(r.Metric);
            return new WebVitalSummaryRow(
                r.RouteTemplate, Enumeration.FromValue<ViewportClass>(r.ViewportClass).DisplayName, metric.DisplayName, r.SampleCount, decimal.Round(r.P75, 4),
                Verdict(metric, r.SampleCount, r.P75));
        }).ToList();

        return new QueryResponse<WebVitalsSummaryViewModel>
        {
            Result = new WebVitalsSummaryViewModel(query.From, query.To, new WebVitalBudgetsViewModel(WebVitalBudgets.LcpMs, WebVitalBudgets.InpMs, WebVitalBudgets.Cls), items)
        };
    }

    /// <summary>Public for the unit tests (U-E3): fewer than 50 samples is never a verdict; the p75 must be at or under the budget to pass.</summary>
    public static string Verdict(WebVitalMetric metric, int sampleCount, decimal p75)
    {
        if (sampleCount < WebVitalBudgets.MinimumSamples) return "InsufficientData";
        var budget = metric == WebVitalMetric.LCP ? WebVitalBudgets.LcpMs : metric == WebVitalMetric.INP ? WebVitalBudgets.InpMs : WebVitalBudgets.Cls;
        return p75 <= budget ? "Pass" : "Fail";
    }
}
