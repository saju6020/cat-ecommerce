using System;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentStaffTime : Migration
    {
        // Written from the enumerations, never literals (U-X3): the occupying predicate is HO-APT-001 section 8.4's.
        private static readonly string OccupyingStatusList = string.Join(", ",
            AppointmentStatus.Held.Value, AppointmentStatus.Booked.Value, AppointmentStatus.CheckedIn.Value, AppointmentStatus.InConsultation.Value);

        /// <summary>The backfill, public so I-A3 can run the very same statement over a table it has emptied.</summary>
        public static readonly string BackfillSql = $"""
            INSERT INTO clinic.appointment_staff_time (id, appointment_id, role, staff_account_id, starts_on, ends_on, occupies)
            SELECT gen_random_uuid(), "Id", {StaffTimeRole.Performer.Value}, performer_id, starts_on, ends_on, TRUE
            FROM clinic.appointment
            WHERE performer_id IS NOT NULL AND starts_on IS NOT NULL AND ends_on IS NOT NULL
              AND status IN ({OccupyingStatusList}) AND NOT is_walk_in;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "assistant_id",
                schema: "clinic",
                table: "appointment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "surgery_id",
                schema: "clinic",
                table: "appointment",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "appointment_staff_time",
                schema: "clinic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    staff_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ends_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    occupies = table.Column<bool>(type: "boolean", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_staff_time", x => x.id);
                    table.ForeignKey(
                        name: "FK_appointment_staff_time_appointment_appointment_id",
                        column: x => x.appointment_id,
                        principalSchema: "clinic",
                        principalTable: "appointment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_surgery_id",
                schema: "clinic",
                table: "appointment",
                column: "surgery_id",
                filter: "surgery_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_staff_time_appointment",
                schema: "clinic",
                table: "appointment_staff_time",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_staff_time_staff_starts_on",
                schema: "clinic",
                table: "appointment_staff_time",
                columns: new[] { "staff_account_id", "starts_on" });

            // HO-SRG-001 section 11.7 G3 (ADR-159): backfill one Performer row per EXISTING occupying appointment, so the new constraint and the
            // performer-busy reads see the bookings made before this migration. Non-occupying appointments get their row the next time they are saved.
            migrationBuilder.Sql(BackfillSql);

            // The fourth exclusion constraint (ADR-159): one staff account, one moment, one occupying appointment -- as performer OR as assistant.
            // Hand-written, like the three on clinic.appointment: EF does not model exclusion constraints (.ai/persistence-ef-core.md).
            migrationBuilder.Sql("""
                ALTER TABLE clinic.appointment_staff_time
                ADD CONSTRAINT ex_appointment_staff_time
                EXCLUDE USING gist (
                    staff_account_id WITH =,
                    tstzrange(starts_on, ends_on, '[)') WITH &&
                ) WHERE (occupies);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE clinic.appointment_staff_time DROP CONSTRAINT IF EXISTS ex_appointment_staff_time;");

            migrationBuilder.DropTable(
                name: "appointment_staff_time",
                schema: "clinic");

            migrationBuilder.DropIndex(
                name: "ix_appointment_surgery_id",
                schema: "clinic",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "assistant_id",
                schema: "clinic",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "surgery_id",
                schema: "clinic",
                table: "appointment");
        }
    }
}
