using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HO-RPT-001 section 12.1 (ADR-278): the `report` schema holds only Reporting's own tables and the read-only views over the other modules. The views are hand-written, versioned SQL;
            // EF maps them keyless with ToView, so a generated migration never contains a CreateTable for one (F-RPT-3).
            migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS report;");

            migrationBuilder.Sql(ReportingViewSql.Load("rv_staff.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_customer.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_branch.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_staff_branch.v1"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_staff_branch"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_branch"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_customer"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_staff"));
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS report;");
        }
    }
}
