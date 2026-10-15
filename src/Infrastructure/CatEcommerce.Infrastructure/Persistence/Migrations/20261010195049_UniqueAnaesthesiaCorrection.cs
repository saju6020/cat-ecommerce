using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueAnaesthesiaCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_anaesthesia_log_entry_corrects",
                schema: "clinic",
                table: "anaesthesia_log_entry");

            migrationBuilder.CreateIndex(
                name: "ix_anaesthesia_log_entry_corrects",
                schema: "clinic",
                table: "anaesthesia_log_entry",
                column: "corrects_entry_id",
                unique: true,
                filter: "corrects_entry_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_anaesthesia_log_entry_corrects",
                schema: "clinic",
                table: "anaesthesia_log_entry");

            migrationBuilder.CreateIndex(
                name: "ix_anaesthesia_log_entry_corrects",
                schema: "clinic",
                table: "anaesthesia_log_entry",
                column: "corrects_entry_id",
                filter: "corrects_entry_id IS NOT NULL");
        }
    }
}
