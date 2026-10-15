using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Dashboard;

/// <summary>One figure of a tile. <c>Value</c> is a number (<c>long</c>), a <see cref="ReportMoney"/> or text; <c>null</c> means "not recorded", never zero.</summary>
public sealed record DashboardFigure(string Key, string Label, object? Value, string Type, string? Unit = null)
{
    public static DashboardFigure Count(string key, string label, long value) => new(key, label, value, "Integer");

    public static DashboardFigure Amount(string key, string label, ReportMoney? value) => new(key, label, value, "Money");

    public static DashboardFigure Text(string key, string label, string? value) => new(key, label, value, "Text");
}

/// <summary>What a tile source returns. <c>DrillFrom</c>/<c>DrillTo</c> give the range of the report the tile opens.</summary>
public sealed record DashboardTileResult(IReadOnlyList<DashboardFigure> Figures, string? Note = null, DateOnly? DrillFrom = null, DateOnly? DrillTo = null);

/// <summary>Everything a tile needs: today at the active branch, the caller, and a way to build the <see cref="ReportRequest"/> of a report whose source the tile reuses (so a tile and its report cannot disagree, I-E1).</summary>
public sealed class DashboardContext(
    CatEcommerceDbContext db, Guid branchId, TimeZoneInfo zone, DateTimeOffset now, Guid callerId, IBusinessSettings settings, string currencyCode)
{
    public CatEcommerceDbContext Db { get; } = db;
    public Guid BranchId { get; } = branchId;
    public TimeZoneInfo Zone { get; } = zone;
    public DateTimeOffset Now { get; } = now;
    public Guid CallerId { get; } = callerId;
    public IBusinessSettings Settings { get; } = settings;
    public string CurrencyCode { get; } = currencyCode;
    public DateOnly Today { get; } = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

    public (DateTimeOffset Start, DateTimeOffset End) Instants(DateOnly from, DateOnly to) => CatEcommerce.Domain.DomainServices.ReportPeriodResolver.ToInstants(from, to, Zone);

    /// <summary>A request for the report <paramref name="key"/> over <paramref name="from"/>..<paramref name="to"/> at the active branch, with every column visible.</summary>
    public ReportRequest Request(string key, DateOnly from, DateOnly to, params (string Name, object Value)[] parameters)
    {
        var definition = ReportCatalogue.Find(key) ?? throw new InvalidOperationException($"No report '{key}'.");
        var (start, end) = Instants(from, to);
        return new ReportRequest(
            definition, from, to, start, end, BranchId, false, Zone, null, parameters.ToDictionary(p => p.Name, p => p.Value),
            definition.Columns.Select(c => c.Key).ToHashSet(StringComparer.Ordinal), ReportRules.CsvMaxRows + 1, Now, CurrencyCode, Settings);
    }
}

/// <summary>
/// HO-RPT-001 section 8.13 -- one class per tile key (U-A2 enforces the 1:1), registered explicitly. A source is invoked <b>only</b> when the caller holds the tile's gate action (U-E2). It reads the report
/// views through the scope and never writes (U-A3).
/// </summary>
public interface IDashboardTileSource
{
    string Key { get; }

    Task<DashboardTileResult> RunAsync(DashboardContext context, CancellationToken ct);
}
