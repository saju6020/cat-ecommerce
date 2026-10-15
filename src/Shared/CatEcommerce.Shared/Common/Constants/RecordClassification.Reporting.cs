using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Reporting (HO-RPT-001) table classifications (HO-XCT-001 §5.2 C-ALL-1), added by the integrator when HO-RPT-001 was merged after HO-XCT-001
/// (2026-10-13). Both tables are aggregate roots that are never deleted by design, so HO-XCT-001 §8.2 rule 5 applies: <see cref="RecordClass.Operational"/>,
/// <see cref="HardDeletePolicy.Forbidden"/> for roots. A schedule ends by <c>Cancel</c> (terminal, HO-RPT-001 §8.9). A run is the run history
/// (§8.9 "Failed runs appear in the run history", §12.4); its purge only removes the generated file and flips the row to <c>Purged</c> (§8.10, ADR-289),
/// and the row backs the exactly-once slot index <c>ix_scheduled_report_run_slot</c> over <c>Generated</c> and <c>Purged</c> runs (ADR-287).
/// §8.2 rule 6 keeps either table from being <see cref="HardDeletePolicy.Purgeable"/> without a new ADR. The <c>report.rv_*</c> views are keyless
/// views, not tables, and are not classified. Recipients are a <c>uuid[]</c> column of staff ids, not a child table.
/// </summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> ReportingEntries =
    [
        new TableClassification("report", "report_schedule", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-RPT-001 §8.9 -- a scheduled report (configuration); paused or cancelled, never deleted"),
        new TableClassification("report", "scheduled_report_run", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-RPT-001 §8.9/§8.10 -- run history; purge removes the file and marks the run Purged, the row stays (ADR-287 slot index)"),
    ];
}
