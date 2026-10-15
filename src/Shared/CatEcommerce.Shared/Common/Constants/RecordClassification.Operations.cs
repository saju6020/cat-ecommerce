using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Cross-cutting / Operations (HO-XCT-001 section 10.3, ADR-297, ADR-298) -- the tables of the <c>ops</c> schema this build adds. A table is added
/// here in the SAME change that adds the table (C-ALL-1); <c>RecordClassificationConventionTests</c> fails on a missing or stale row. Other
/// areas also use schema <c>ops</c> (Ordering: <c>idempotency_record</c>, <c>inbound_webhook_receipt</c>); those rows stay in Ordering own file.
/// </summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> OperationsEntries =
    [
        // Phase B -- the erasure ledger holds no personal data and is the proof a request was honoured (ADR-302): Regulatory, never deleted.
        new TableClassification("ops", "erasure_request", RecordClass.Regulatory, HardDeletePolicy.Forbidden, "HO-XCT-001 ADR-302 -- proof that an erasure request was honoured"),
        new TableClassification("ops", "erasure_scope", RecordClass.Regulatory, HardDeletePolicy.Forbidden, "HO-XCT-001 ADR-302 -- one context progress through an erasure request"),
        // Phase C -- each run is an audit-grade log of what the retention worker did (ADR-303); the summary row replaces change capture (section 5.3).
        new TableClassification("ops", "retention_run", RecordClass.Audit, HardDeletePolicy.Forbidden, "HO-XCT-001 ADR-303 -- what the retention worker did, indefinitely (D-5)"),
        new TableClassification("ops", "retention_pass_result", RecordClass.Audit, HardDeletePolicy.Forbidden, "HO-XCT-001 ADR-303 -- one pass of a retention run"),
        // Phase E -- anonymous real-user telemetry, purged by RetentionPurger after retention.webVitalsDays (ADR-309).
        new TableClassification("ops", "web_vital_batch", RecordClass.Telemetry, HardDeletePolicy.Purgeable, "HO-XCT-001 ADR-309 -- anonymous web-vital beacon, purged after retention.webVitalsDays"),
        new TableClassification("ops", "web_vital_sample", RecordClass.Telemetry, HardDeletePolicy.Purgeable, "HO-XCT-001 ADR-309 -- one anonymous measurement, deleted with its batch"),
        // Phase F -- the restore drills are an audit-grade record (ADR-313); backup_run is the backup job log, written only by the job (ADR-312).
        new TableClassification("ops", "restore_test_record", RecordClass.Audit, HardDeletePolicy.Forbidden, "HO-XCT-001 ADR-313 -- proof a restore was tested, append-only"),
        new TableClassification("ops", "restore_check_result", RecordClass.Audit, HardDeletePolicy.Forbidden, "HO-XCT-001 ADR-313 -- one named check of a restore drill"),
        new TableClassification("ops", "backup_run", RecordClass.Telemetry, HardDeletePolicy.Purgeable, "HO-XCT-001 ADR-312 -- backup job log, purged after RetentionRules.BackupRunDays"),
    ];
}
