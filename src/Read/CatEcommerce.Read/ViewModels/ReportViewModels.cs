using CatEcommerce.Domain.Models;

namespace CatEcommerce.Read.ViewModels;

// HO-RPT-001 section 12.4 -- the contract HO-RPT-002 consumes. The raw model (ReportResult, in Domain.Models so the Infrastructure CSV writer and PDF renderer can read it) is one result in three
// shapes -- screen, CSV, PDF (ADR-285). ReportResultFormatter turns it into the JSON shape.

/// <summary>A report result, or the reason it was refused (the controller maps the code to 400, 403, 404 or 413).</summary>
public sealed record ReportOutcome(ReportResult? Result, string? RefusalCode, string? RefusalMessage, string? RefusalProperty)
{
    public bool IsRefused => RefusalCode is not null;

    public static ReportOutcome Success(ReportResult result) => new(result, null, null, null);

    public static ReportOutcome Refused(string code, string message, string? property = null) => new(null, code, message, property);
}

// ── Catalogue ────────────────────────────────────────────────────────────────────────────────────────

public sealed record ReportDateRangeViewModel(DateOnly From, DateOnly To);

public sealed record ReportParameterViewModel(
    string Name, string Label, string Type, IReadOnlyList<string>? AllowedValues, int? Min, int? Max, string Default);

public sealed record ReportColumnViewModel(string Key, string Label, string Type, string? Unit, bool Sortable, string? Description = null);

public sealed record ReportCatalogueEntryViewModel(
    string Key, string Title, string Description, string Category, string Kind, bool TimeBucketed, string DefaultPeriod, ReportDateRangeViewModel DefaultRange,
    int MaxRangeDays, IReadOnlyList<string> Formats, IReadOnlyList<ReportParameterViewModel> Parameters, IReadOnlyList<ReportColumnViewModel> Columns,
    bool CanUseAllBranches, bool CanSchedule);

// ── Dashboard (section 12.4) ─────────────────────────────────────────────────────────────────────────

public sealed record DashboardFigureViewModel(string Key, string Label, object? Value, string Type, string? Unit);

public sealed record DashboardDrillDownViewModel(string ReportKey, DateOnly From, DateOnly To, IReadOnlyDictionary<string, string> Parameters);

public sealed record DashboardTileViewModel(string Key, string Title, IReadOnlyList<DashboardFigureViewModel> Figures, DashboardDrillDownViewModel? DrillDown, string? Note);

public sealed record DashboardViewModel(
    Guid BranchId, string BranchName, DateOnly LocalDate, string TimeZoneId, DateTimeOffset GeneratedOn, int RefreshSeconds, IReadOnlyList<DashboardTileViewModel> Tiles);

/// <summary>The dashboard, or the refusal (no resolvable branch).</summary>
public sealed record DashboardOutcome(DashboardViewModel? Result, string? RefusalCode)
{
    public bool IsRefused => RefusalCode is not null;
}
