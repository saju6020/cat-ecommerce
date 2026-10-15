using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReportViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportingViewSql.Load("rv_stock_ledger.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_batch.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_stock_location.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_consignment.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_sku.v1"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_sku"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_consignment"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_stock_location"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_batch"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_stock_ledger"));
        }
    }
}
