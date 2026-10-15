using System;
using CatEcommerce.Infrastructure.Ordering;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderingOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_order",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfilment_method = table.Column<int>(type: "integer", nullable: false),
                    payment_method = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recipient_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    address_line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    delivery_instructions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    delivery_zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zone_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    service_level = table.Column<int>(type: "integer", nullable: true),
                    slot_date = table.Column<DateOnly>(type: "date", nullable: true),
                    slot_window_label = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    slot_window_start = table.Column<TimeOnly>(type: "time", nullable: true),
                    slot_window_end = table.Column<TimeOnly>(type: "time", nullable: true),
                    pickup_branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    line_discount_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    order_discount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    delivery_charge = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    grand_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    discount_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    discount_code_text = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    discount_released = table.Column<bool>(type: "boolean", nullable: false),
                    quote_fingerprint = table.Column<string>(type: "char(64)", nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: true),
                    placed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    payment_due_by = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    customer_transfer_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    transfer_reported_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    held_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    held_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    held_received_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    held_recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    held_recorded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invoice_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    credit_note_ids = table.Column<string>(type: "jsonb", nullable: false),
                    amount_paid = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    amount_refunded = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    refund_due = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    shipment_courier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shipment_courier_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    shipment_tracking_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    shipment_tracking_url = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: true),
                    shipment_dispatched_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    shipment_dispatched_by = table.Column<Guid>(type: "uuid", nullable: true),
                    shipment_last_status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    shipment_last_event_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ready_for_collection_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    collect_by_date = table.Column<DateOnly>(type: "date", nullable: true),
                    delivered_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    delivery_recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cod_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    cod_collector_kind = table.Column<int>(type: "integer", nullable: true),
                    cod_collector_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cod_collector_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cod_collected_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cod_recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    delivery_failure_reason = table.Column<int>(type: "integer", nullable: true),
                    cancellation_reason = table.Column<int>(type: "integer", nullable: true),
                    cancellation_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_by_customer = table.Column<bool>(type: "boolean", nullable: false),
                    confirmed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    packing_started_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    customer_anonymised = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: true),
                    VerticalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<string>(type: "text", nullable: true),
                    IsMarkedToDelete = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "order_line",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    variant_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    barcode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    return_class = table.Column<int>(type: "integer", nullable: false),
                    is_bundle = table.Column<bool>(type: "boolean", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    line_subtotal = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    line_discount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    order_discount_share = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    tax_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_rate_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    quantity_returned = table.Column<int>(type: "integer", nullable: false),
                    components = table.Column<string>(type: "jsonb", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_line_sales_order_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "sales_order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_payment_ref",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    recorded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_payment_ref", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_payment_ref_sales_order_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "sales_order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_status_change",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<int>(type: "integer", nullable: true),
                    to_status = table.Column<int>(type: "integer", nullable: false),
                    changed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_change", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_status_change_sales_order_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "sales_order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_line_allocation",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(14,3)", nullable: false),
                    batch_expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_line_allocation", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_line_allocation_order_line_order_line_id",
                        column: x => x.order_line_id,
                        principalSchema: "ordering",
                        principalTable: "order_line",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_line_order",
                schema: "ordering",
                table: "order_line",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_line_allocation_batch",
                schema: "ordering",
                table: "order_line_allocation",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_line_allocation_order_line_id",
                schema: "ordering",
                table: "order_line_allocation",
                column: "order_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_payment_ref_order",
                schema: "ordering",
                table: "order_payment_ref",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_status_change_order",
                schema: "ordering",
                table: "order_status_change",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_customer_idempotency",
                schema: "ordering",
                table: "sales_order",
                columns: new[] { "customer_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_customer_placed",
                schema: "ordering",
                table: "sales_order",
                columns: new[] { "customer_id", "placed_on" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_discount_customer",
                schema: "ordering",
                table: "sales_order",
                columns: new[] { "discount_code_id", "customer_id" },
                filter: "discount_code_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_expiry",
                schema: "ordering",
                table: "sales_order",
                columns: new[] { "status", "payment_method", "payment_due_by" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_number",
                schema: "ordering",
                table: "sales_order",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_queue",
                schema: "ordering",
                table: "sales_order",
                columns: new[] { "branch_id", "status", "placed_on" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_tracking",
                schema: "ordering",
                table: "sales_order",
                column: "shipment_tracking_number",
                filter: "shipment_tracking_number IS NOT NULL");

            // V1: the order-number sequence (nextval does not roll back, so numbers may have gaps), and D-5/D-6/D-9: orders are never deleted -- row
            // triggers refuse DELETE, the append-only histories refuse UPDATE, and BEFORE TRUNCATE statement triggers close the door row triggers leave open.
            migrationBuilder.Sql(OrderingMigrationSql.CreateOrderNumberSequence());
            migrationBuilder.Sql(OrderingMigrationSql.ImmutabilityTriggers());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(OrderingMigrationSql.DropImmutabilityTriggers());
            migrationBuilder.Sql(OrderingMigrationSql.DropOrderNumberSequence());

            migrationBuilder.DropTable(
                name: "order_line_allocation",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "order_payment_ref",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "order_status_change",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "order_line",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "sales_order",
                schema: "ordering");
        }
    }
}
