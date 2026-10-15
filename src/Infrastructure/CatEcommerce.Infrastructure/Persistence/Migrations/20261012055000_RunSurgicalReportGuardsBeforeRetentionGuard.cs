using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Integrator migration (merge of HO-XCT-001 after HO-SRG-001, 2026-10-13). PostgreSQL fires triggers of the same timing in name order, so once
    /// <c>AttachRetentionGuardsToSurgeryTables</c> added <c>trg_refuse_hard_delete</c> / <c>trg_refuse_truncate</c> to the surgical report tables, the
    /// retention guard ran before SRG's own immutability triggers and a raw DELETE answered "Hard delete of ..." instead of SRG's "immutable: ..."
    /// (SurgeryReportEndpointTests.I_E3). This only renames SRG's four triggers so they sort, and therefore fire, before the retention pair. No trigger,
    /// function or row is added, removed or changed; both guards still refuse. The anaesthesia log's triggers already sort first and are not touched.
    /// </summary>
    public partial class RunSurgicalReportGuardsBeforeRetentionGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TRIGGER trg_surgical_report_immutable ON clinic.surgical_report RENAME TO trg_immutable_surgical_report;
                ALTER TRIGGER trg_surgical_report_no_truncate ON clinic.surgical_report RENAME TO trg_no_truncate_surgical_report;
                ALTER TRIGGER trg_surgical_report_addendum_immutable ON clinic.surgical_report_addendum RENAME TO trg_immutable_surgical_report_addendum;
                ALTER TRIGGER trg_surgical_report_addendum_no_truncate ON clinic.surgical_report_addendum RENAME TO trg_no_truncate_surgical_report_addendum;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TRIGGER trg_no_truncate_surgical_report_addendum ON clinic.surgical_report_addendum RENAME TO trg_surgical_report_addendum_no_truncate;
                ALTER TRIGGER trg_immutable_surgical_report_addendum ON clinic.surgical_report_addendum RENAME TO trg_surgical_report_addendum_immutable;
                ALTER TRIGGER trg_no_truncate_surgical_report ON clinic.surgical_report RENAME TO trg_surgical_report_no_truncate;
                ALTER TRIGGER trg_immutable_surgical_report ON clinic.surgical_report RENAME TO trg_surgical_report_immutable;
                """);
        }
    }
}
