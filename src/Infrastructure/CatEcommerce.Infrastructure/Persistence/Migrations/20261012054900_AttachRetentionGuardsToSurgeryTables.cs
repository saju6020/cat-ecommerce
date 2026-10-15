using CatEcommerce.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Integrator migration (merge of HO-XCT-001 after HO-SRG-001, 2026-10-13). HO-SRG-001's Forbidden clinic tables were created after
    /// <c>AddRetentionGuards</c> froze its table list, so they never received the ADR-299 layer-1 triggers. This attaches the BEFORE DELETE row trigger
    /// and the BEFORE TRUNCATE statement trigger to each of them (C-ALL-1). The frozen list in <c>AddRetentionGuards</c> is not touched.
    /// The anaesthesia log, the surgical report and its addenda keep their own immutability triggers as well; the retention pair is added beside them.
    /// </summary>
    public partial class AttachRetentionGuardsToSurgeryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("clinic", "surgery"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("clinic", "surgery_checklist_item"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("clinic", "surgery_follow_up"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("clinic", "surgery_status_change"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("clinic", "surgery_recovery_change"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("clinic", "anaesthesia_log_entry"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("clinic", "surgical_report"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("clinic", "surgical_report_addendum"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("clinic", "surgery_protocol"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("clinic", "surgery_campaign"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("clinic", "surgery_campaign"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("clinic", "surgery_protocol"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("clinic", "surgical_report_addendum"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("clinic", "surgical_report"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("clinic", "anaesthesia_log_entry"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("clinic", "surgery_recovery_change"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("clinic", "surgery_status_change"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("clinic", "surgery_follow_up"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("clinic", "surgery_checklist_item"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("clinic", "surgery"));
        }
    }
}
