using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardReportViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportingViewSql.Load("rv_order.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_order_return.v1"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_order_return"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_order"));
        }
    }
}
