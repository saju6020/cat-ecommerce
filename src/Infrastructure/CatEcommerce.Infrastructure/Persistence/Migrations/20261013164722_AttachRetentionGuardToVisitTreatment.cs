using CatEcommerce.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Integrator migration (merge of HO-EMR-001-F after HO-XCT-001, 2026-10-13). <c>clinic.visit_treatment</c> is classified Forbidden but was added
    /// after <c>AddRetentionGuards</c> froze its list, so it never received the ADR-299 layer-1 pair (U-A7 / I-A3). This attaches the BEFORE DELETE row
    /// trigger and the BEFORE TRUNCATE statement trigger (C-ALL-1). EMR's own <c>tr_visit_treatment_frozen</c> / <c>tr_visit_treatment_no_truncate</c>
    /// sort before <c>trg_refuse_*</c>, so they still fire first and keep their messages.
    /// </summary>
    public partial class AttachRetentionGuardToVisitTreatment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("clinic", "visit_treatment"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("clinic", "visit_treatment"));
        }
    }
}
