using System;
using CatEcommerce.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardingWaitlistAndDaycare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daycare_rate_card",
                schema: "boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hourly_rate = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    day_rate = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    tax_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    set_by = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_daycare_rate_card", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "daycare_visit",
                schema: "boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    slot_number = table.Column<int>(type: "integer", nullable: false),
                    basis = table.Column<int>(type: "integer", nullable: false),
                    expected_hours = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    hourly_rate = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    day_rate = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    tax_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    booked_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    checked_in_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    checked_out_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    charge_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
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
                    table.PrimaryKey("PK_daycare_visit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "waitlist_entry",
                schema: "boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    arrival_on = table.Column<DateOnly>(type: "date", nullable: false),
                    departure_on = table.Column<DateOnly>(type: "date", nullable: false),
                    cat_count = table.Column<int>(type: "integer", nullable: false),
                    unit_type = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    joined_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    notified_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ended_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
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
                    table.PrimaryKey("PK_waitlist_entry", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_daycare_rate_card_effective",
                schema: "boarding",
                table: "daycare_rate_card",
                columns: new[] { "branch_id", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_daycare_slot",
                schema: "boarding",
                table: "daycare_visit",
                columns: new[] { "branch_id", "local_date", "slot_number" },
                unique: true,
                filter: "status <> 4");

            migrationBuilder.CreateIndex(
                name: "ix_daycare_visit_cat_date",
                schema: "boarding",
                table: "daycare_visit",
                columns: new[] { "cat_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "ix_daycare_visit_customer_date",
                schema: "boarding",
                table: "daycare_visit",
                columns: new[] { "customer_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "ix_boarding_waitlist_active",
                schema: "boarding",
                table: "waitlist_entry",
                columns: new[] { "customer_id", "arrival_on", "departure_on" },
                unique: true,
                filter: "status IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "ix_waitlist_entry_pass",
                schema: "boarding",
                table: "waitlist_entry",
                columns: new[] { "branch_id", "arrival_on", "status", "joined_on" });
            // C-ALL-1 / ADR-299 layer 1: every Forbidden table carries the retention trigger pair (U-A7 / I-A3). Literal calls: U-A7 scans for them.
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "waitlist_entry"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "daycare_rate_card"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "daycare_visit"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "daycare_visit"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "daycare_rate_card"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "waitlist_entry"));

            migrationBuilder.DropTable(
                name: "daycare_rate_card",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "daycare_visit",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "waitlist_entry",
                schema: "boarding");
        }
    }
}
