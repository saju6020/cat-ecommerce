using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderDeliveryTaxAndPaymentWaiver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "delivery_tax_amount",
                schema: "ordering",
                table: "sales_order",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "approval_waiver_reason",
                schema: "billing",
                table: "payment",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "delivery_tax_amount",
                schema: "ordering",
                table: "sales_order");

            migrationBuilder.DropColumn(
                name: "approval_waiver_reason",
                schema: "billing",
                table: "payment");
        }
    }
}
