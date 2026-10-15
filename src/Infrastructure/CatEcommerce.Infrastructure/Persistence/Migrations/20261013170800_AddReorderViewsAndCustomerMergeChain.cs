using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// HO-RPT-001 gate fix round: R7 reads views (not IMP's tables), and rv_customer_activity follows a whole merge chain (version 2). Views in schema `report` only (U-X5); no table changes.
    /// </summary>
    public partial class AddReorderViewsAndCustomerMergeChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_customer_activity"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_customer_activity.v2", ("COUNTED_STATUSES", CountedStatuses)));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_stock_position.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_reorder_policy.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_on_order.v1", ("ISSUED", PurchaseOrderStatus.Issued.Value.ToString())));
        }

        private static readonly string CountedStatuses = ReportingViewSql.List(InvoiceStatus.Issued, InvoiceStatus.PartPaid, InvoiceStatus.Paid, InvoiceStatus.Credited);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_on_order"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_reorder_policy"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_stock_position"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_customer_activity"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_customer_activity.v1", ("COUNTED_STATUSES", CountedStatuses)));
        }
    }
}
