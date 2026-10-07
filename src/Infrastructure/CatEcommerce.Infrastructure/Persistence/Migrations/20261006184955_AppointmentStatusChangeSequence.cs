using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentStatusChangeSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sequence",
                schema: "clinic",
                table: "appointment_status_change",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill existing rows with a per-appointment ordinal (changed_on, then id as the
            // best available stand-in for insertion order) so history already stored sorts stably.
            migrationBuilder.Sql("""
                UPDATE clinic.appointment_status_change AS c
                SET sequence = r.rn
                FROM (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY appointment_id ORDER BY changed_on, id) AS rn
                    FROM clinic.appointment_status_change
                ) AS r
                WHERE c.id = r.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sequence",
                schema: "clinic",
                table: "appointment_status_change");
        }
    }
}
