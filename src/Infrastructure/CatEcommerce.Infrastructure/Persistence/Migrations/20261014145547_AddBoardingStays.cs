using System;
using CatEcommerce.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardingStays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stay",
                schema: "boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    arrival_on = table.Column<DateOnly>(type: "date", nullable: false),
                    departure_on = table.Column<DateOnly>(type: "date", nullable: false),
                    occupied_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    occupied_to = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    requested_unit_type = table.Column<int>(type: "integer", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    requires_isolation = table.Column<bool>(type: "boolean", nullable: false),
                    rate_card_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rate_card_unit_type = table.Column<int>(type: "integer", nullable: true),
                    rate_card_nightly = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    rate_card_weekly = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    rate_card_monthly = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    rate_card_additional_cat = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    rate_card_tax_class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rate_card_currency_code = table.Column<string>(type: "char(3)", nullable: true),
                    pricing_long_stay_threshold_nights = table.Column<int>(type: "integer", nullable: true),
                    pricing_long_stay_discount_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    pricing_early_checkout_charged_nights = table.Column<int>(type: "integer", nullable: true),
                    pricing_early_checkout_refund_as_credit = table.Column<bool>(type: "boolean", nullable: true),
                    rate_basis = table.Column<int>(type: "integer", nullable: true),
                    quote_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    discount_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    deposit_required = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    deposit_due_by = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    deposit_state = table.Column<int>(type: "integer", nullable: false),
                    reserved_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    confirmed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    checked_in_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    checked_out_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelled_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<int>(type: "integer", nullable: true),
                    cancellation_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    deposit_forfeited = table.Column<bool>(type: "boolean", nullable: false),
                    no_show_recorded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    no_show_recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    needs_attention = table.Column<bool>(type: "boolean", nullable: false),
                    attention_reason = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_stay", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stay_cat",
                schema: "boarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occupied_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    occupied_to = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    gate_status = table.Column<int>(type: "integer", nullable: false),
                    gate_reasons = table.Column<string>(type: "jsonb", nullable: false),
                    gate_evaluated_on = table.Column<DateOnly>(type: "date", nullable: true),
                    gate_overridden_by = table.Column<Guid>(type: "uuid", nullable: true),
                    gate_overridden_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    gate_override_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    has_care_profile = table.Column<bool>(type: "boolean", nullable: false),
                    litter_type = table.Column<int>(type: "integer", nullable: true),
                    litter_note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    vet_visit_authorised = table.Column<bool>(type: "boolean", nullable: false),
                    vet_spending_limit = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    emergency_contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    emergency_contact_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    emergency_contact_relationship = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    care_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stay_cat", x => x.id);
                    table.ForeignKey(
                        name: "FK_stay_cat_stay_stay_id",
                        column: x => x.stay_id,
                        principalSchema: "boarding",
                        principalTable: "stay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stay_rate_unit",
                schema: "boarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    from_night = table.Column<int>(type: "integer", nullable: false),
                    to_night = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    discount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stay_rate_unit", x => x.id);
                    table.ForeignKey(
                        name: "FK_stay_rate_unit_stay_stay_id",
                        column: x => x.stay_id,
                        principalSchema: "boarding",
                        principalTable: "stay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stay_status_change",
                schema: "boarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<int>(type: "integer", nullable: false),
                    to_status = table.Column<int>(type: "integer", nullable: false),
                    changed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stay_status_change", x => x.id);
                    table.ForeignKey(
                        name: "FK_stay_status_change_stay_stay_id",
                        column: x => x.stay_id,
                        principalSchema: "boarding",
                        principalTable: "stay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stay_unit_allocation",
                schema: "boarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_type = table.Column<int>(type: "integer", nullable: false),
                    occupied_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    occupied_to = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stay_unit_allocation", x => x.id);
                    table.ForeignKey(
                        name: "FK_stay_unit_allocation_stay_stay_id",
                        column: x => x.stay_id,
                        principalSchema: "boarding",
                        principalTable: "stay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stay_cat_feeding",
                schema: "boarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    portion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    food_source = table.Column<int>(type: "integer", nullable: false),
                    food_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    house_food_sku_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stay_cat_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stay_cat_feeding", x => x.id);
                    table.ForeignKey(
                        name: "FK_stay_cat_feeding_stay_cat_stay_cat_id",
                        column: x => x.stay_cat_id,
                        principalSchema: "boarding",
                        principalTable: "stay_cat",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stay_cat_medication",
                schema: "boarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    route = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    times = table.Column<TimeOnly[]>(type: "time[]", nullable: false),
                    instructions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    from_on = table.Column<DateOnly>(type: "date", nullable: true),
                    to_on = table.Column<DateOnly>(type: "date", nullable: true),
                    stay_cat_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stay_cat_medication", x => x.id);
                    table.ForeignKey(
                        name: "FK_stay_cat_medication_stay_cat_stay_cat_id",
                        column: x => x.stay_cat_id,
                        principalSchema: "boarding",
                        principalTable: "stay_cat",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stay_branch_arrival",
                schema: "boarding",
                table: "stay",
                columns: new[] { "branch_id", "arrival_on" });

            migrationBuilder.CreateIndex(
                name: "ix_stay_branch_departure",
                schema: "boarding",
                table: "stay",
                columns: new[] { "branch_id", "departure_on" });

            migrationBuilder.CreateIndex(
                name: "ix_stay_branch_status",
                schema: "boarding",
                table: "stay",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_stay_customer_arrival",
                schema: "boarding",
                table: "stay",
                columns: new[] { "customer_id", "arrival_on" });

            migrationBuilder.CreateIndex(
                name: "ix_stay_reserved_deposit_due",
                schema: "boarding",
                table: "stay",
                columns: new[] { "status", "deposit_due_by" },
                filter: "status = 2");

            migrationBuilder.CreateIndex(
                name: "ix_stay_cat_cat",
                schema: "boarding",
                table: "stay_cat",
                column: "cat_id");

            migrationBuilder.CreateIndex(
                name: "ix_stay_cat_stay",
                schema: "boarding",
                table: "stay_cat",
                column: "stay_id");

            migrationBuilder.CreateIndex(
                name: "ix_stay_cat_feeding_stay_cat",
                schema: "boarding",
                table: "stay_cat_feeding",
                column: "stay_cat_id");

            migrationBuilder.CreateIndex(
                name: "ix_stay_cat_medication_stay_cat",
                schema: "boarding",
                table: "stay_cat_medication",
                column: "stay_cat_id");

            migrationBuilder.CreateIndex(
                name: "ix_stay_rate_unit_stay",
                schema: "boarding",
                table: "stay_rate_unit",
                column: "stay_id");

            migrationBuilder.CreateIndex(
                name: "ix_stay_status_change_stay",
                schema: "boarding",
                table: "stay_status_change",
                column: "stay_id");

            migrationBuilder.CreateIndex(
                name: "ix_stay_unit_allocation_stay",
                schema: "boarding",
                table: "stay_unit_allocation",
                column: "stay_id");

            migrationBuilder.CreateIndex(
                name: "ix_stay_unit_allocation_unit_from",
                schema: "boarding",
                table: "stay_unit_allocation",
                columns: new[] { "unit_id", "occupied_from" });

            // ── Raw SQL EF cannot model (read before committing -- HO-BRD-001 section 17) ───────────────────────────
            // btree_gist is already installed by AddBoardingUnits (idempotent here too). ADR-219 (renumbered ADR-352): the OCCUPANCY control. [) -- back-to-back
            // ranges are fine; the predicate is the is_active column the aggregate maintains in every transition (F31), true from Reserved to CheckedOut.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql("""
                ALTER TABLE boarding.stay_unit_allocation
                ADD CONSTRAINT ex_stay_allocation_unit
                EXCLUDE USING gist (
                    "unit_id" WITH =,
                    tstzrange("occupied_from", "occupied_to", '[)') WITH &&
                ) WHERE ("is_active");
                """);

            // BR-13 / D-7: one cat in one live stay at a time. A clinic appointment is another table, so a stay and a visit may overlap by construction.
            migrationBuilder.Sql("""
                ALTER TABLE boarding.stay_cat
                ADD CONSTRAINT ex_stay_cat
                EXCLUDE USING gist (
                    "cat_id" WITH =,
                    tstzrange("occupied_from", "occupied_to", '[)') WITH &&
                ) WHERE ("is_active");
                """);

            // C-ALL-1 / ADR-299 layer 1: every Forbidden table carries the retention trigger pair (U-A7 / I-A3). Literal calls: U-A7 scans for them.
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "stay"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "stay_status_change"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "stay_unit_allocation"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "stay_unit_allocation"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "stay_status_change"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "stay"));
            migrationBuilder.Sql("ALTER TABLE boarding.stay_cat DROP CONSTRAINT IF EXISTS ex_stay_cat;");
            migrationBuilder.Sql("ALTER TABLE boarding.stay_unit_allocation DROP CONSTRAINT IF EXISTS ex_stay_allocation_unit;");

            migrationBuilder.DropTable(
                name: "stay_cat_feeding",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "stay_cat_medication",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "stay_rate_unit",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "stay_status_change",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "stay_unit_allocation",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "stay_cat",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "stay",
                schema: "boarding");
        }
    }
}
