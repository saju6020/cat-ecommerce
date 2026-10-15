using CatEcommerce.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Integrator migration (merge of HO-RPT-001 after HO-XCT-001, 2026-10-13). <c>report.report_schedule</c> and <c>report.scheduled_report_run</c> are
    /// classified Operational / Forbidden (<c>RecordClassification.Reporting.cs</c>) and postdate <c>AddRetentionGuards</c>' frozen list, so they get the
    /// ADR-299 layer-1 pair here (C-ALL-1; U-A7 / I-A3). Neither table had a delete or truncate trigger of its own.
    /// </summary>
    public partial class AttachRetentionGuardsToReportingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("report", "report_schedule"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("report", "scheduled_report_run"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("report", "scheduled_report_run"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("report", "report_schedule"));
        }
    }
}
