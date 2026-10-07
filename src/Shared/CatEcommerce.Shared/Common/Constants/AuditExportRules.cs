namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-ADM-001 §10.3 / ADR-051.</summary>
public static class AuditExportRules
{
    public const int MaxRows = 50_000;
    public const int MaxDateRangeDays = 366;

    /// <summary>Gate fix 2026-09-26: the interactive search page (never the CSV export, which
    /// still uses <see cref="MaxRows"/>) is capped at this size -- an unbounded page size lets a
    /// caller pull the whole export's worth of rows through the paged endpoint instead.</summary>
    public const int MaxSearchPageSize = 200;
}
