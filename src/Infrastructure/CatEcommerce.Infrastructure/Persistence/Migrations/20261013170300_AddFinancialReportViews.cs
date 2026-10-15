using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialReportViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportingViewSql.Load("rv_invoice_line.v1", ("ORDER_SOURCE", ChargeSourceKind.Order.Value.ToString()), ("APPOINTMENT_SOURCE", ChargeSourceKind.Appointment.Value.ToString())));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_credit_note_line.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_credit_note.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_refund.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_payment.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_cash_up.v1"));
            migrationBuilder.Sql(ReportingViewSql.Load("rv_invoice_balance.v1", ("RECEIVABLE_STATUSES", ReportingViewSql.List(InvoiceStatus.Issued, InvoiceStatus.PartPaid, InvoiceStatus.Paid, InvoiceStatus.Credited))));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_invoice_balance"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_cash_up"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_payment"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_refund"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_credit_note"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_credit_note_line"));
            migrationBuilder.Sql(ReportingViewSql.Drop("rv_invoice_line"));
        }
    }
}
