using System;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// HO-APT-001 §12.1 (ADR-068, F28/F29). Installs <c>btree_gist</c> (trusted since PostgreSQL 13,
    /// F29 -- no superuser required) and the three exclusion constraints that are the sole control
    /// against double booking: no application lock, no read-then-write check. The status values in
    /// each predicate are <see cref="Enumeration.Value"/> integers taken from
    /// <see cref="AppointmentStatus"/> at migration-authoring time (U-X3 checks the constraint text
    /// against the enumeration) -- never typed as bare literals.
    /// </summary>
    public partial class AddClinicAppointments : Migration
    {
        private static readonly string OccupyingStatusList = string.Join(", ",
            AppointmentStatus.Held.Value, AppointmentStatus.Booked.Value, AppointmentStatus.CheckedIn.Value, AppointmentStatus.InConsultation.Value);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Down does NOT drop this extension -- another object may use it (§12.1).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.CreateTable(
                name: "appointment",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    service_category = table.Column<int>(type: "integer", nullable: false),
                    service_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    price_currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    performer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    room_id = table.Column<Guid>(type: "uuid", nullable: true),
                    starts_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ends_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_walk_in = table.Column<bool>(type: "boolean", nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    hold_expires_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    waitlist_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_for_visit = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    triage_priority = table.Column<int>(type: "integer", nullable: false),
                    presenting_complaint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    checked_in_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    started_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    completed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelled_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_by_customer = table.Column<bool>(type: "boolean", nullable: false),
                    no_show_recorded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    no_show_recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reschedule_count = table.Column<int>(type: "integer", nullable: false),
                    schedule_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    needs_rescheduling = table.Column<bool>(type: "boolean", nullable: false),
                    reschedule_flag_reason = table.Column<int>(type: "integer", nullable: true),
                    reschedule_flag_source_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_appointment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "appointment_status_change",
                schema: "clinic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<int>(type: "integer", nullable: false),
                    to_status = table.Column<int>(type: "integer", nullable: false),
                    changed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_status_change", x => x.id);
                    table.ForeignKey(
                        name: "FK_appointment_status_change_appointment_appointment_id",
                        column: x => x.appointment_id,
                        principalSchema: "clinic",
                        principalTable: "appointment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_branch_starts_on",
                schema: "clinic",
                table: "appointment",
                columns: new[] { "branch_id", "starts_on" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_branch_status",
                schema: "clinic",
                table: "appointment",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_customer_starts_on",
                schema: "clinic",
                table: "appointment",
                columns: new[] { "customer_id", "starts_on" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_customer_status",
                schema: "clinic",
                table: "appointment",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_needs_rescheduling_branch",
                schema: "clinic",
                table: "appointment",
                columns: new[] { "needs_rescheduling", "branch_id" },
                filter: "needs_rescheduling");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_performer_starts_on",
                schema: "clinic",
                table: "appointment",
                columns: new[] { "performer_id", "starts_on" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_room_starts_on",
                schema: "clinic",
                table: "appointment",
                columns: new[] { "room_id", "starts_on" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_status_hold_expires_on",
                schema: "clinic",
                table: "appointment",
                columns: new[] { "status", "hold_expires_on" });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_status_change_appointment_id",
                schema: "clinic",
                table: "appointment_status_change",
                column: "appointment_id");

            // ADR-068: the three exclusion constraints. tstzrange(..., '[)') -- back-to-back slots
            // are fine (F28); a stale Held row still matches (it cannot call now()) -- released
            // lazily by every write that overlaps it (ADR-070).
            migrationBuilder.Sql($"""
                ALTER TABLE clinic.appointment
                ADD CONSTRAINT ex_appointment_performer
                EXCLUDE USING gist (
                    performer_id WITH =,
                    tstzrange(starts_on, ends_on, '[)') WITH &&
                ) WHERE (status IN ({OccupyingStatusList}) AND NOT is_walk_in);
                """);

            migrationBuilder.Sql($"""
                ALTER TABLE clinic.appointment
                ADD CONSTRAINT ex_appointment_room
                EXCLUDE USING gist (
                    room_id WITH =,
                    tstzrange(starts_on, ends_on, '[)') WITH &&
                ) WHERE (status IN ({OccupyingStatusList}) AND NOT is_walk_in);
                """);

            migrationBuilder.Sql($"""
                ALTER TABLE clinic.appointment
                ADD CONSTRAINT ex_appointment_cat
                EXCLUDE USING gist (
                    cat_id WITH =,
                    tstzrange(starts_on, ends_on, '[)') WITH &&
                ) WHERE (status IN ({OccupyingStatusList}) AND NOT is_walk_in);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE clinic.appointment DROP CONSTRAINT IF EXISTS ex_appointment_cat;");
            migrationBuilder.Sql("ALTER TABLE clinic.appointment DROP CONSTRAINT IF EXISTS ex_appointment_room;");
            migrationBuilder.Sql("ALTER TABLE clinic.appointment DROP CONSTRAINT IF EXISTS ex_appointment_performer;");

            migrationBuilder.DropTable(
                name: "appointment_status_change",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "appointment",
                schema: "clinic");

            // btree_gist is NOT dropped -- another object may use it (§12.1).
        }
    }
}
