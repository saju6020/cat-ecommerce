using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGrowthReportViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportingViewSql.Load("rv_customer_activity.v1", ("COUNTED_STATUSES", ReportingViewSql.List(InvoiceStatus.Issued, InvoiceStatus.PartPaid, InvoiceStatus.Paid, InvoiceStatus.Credited))));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_customer_activity"));
        }
    }
}
