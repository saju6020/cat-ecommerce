using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSurgeryProtocols : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "surgery_protocol",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fasting_instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    fasting_hours = table.Column<int>(type: "integer", nullable: false),
                    post_op_instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
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
                    table.PrimaryKey("PK_surgery_protocol", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "surgery_protocol_follow_up",
                schema: "clinic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    day_offset = table.Column<int>(type: "integer", nullable: false),
                    window_from_day = table.Column<int>(type: "integer", nullable: false),
                    window_to_day = table.Column<int>(type: "integer", nullable: false),
                    protocol_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_surgery_protocol_follow_up", x => x.id);
                    table.ForeignKey(
                        name: "FK_surgery_protocol_follow_up_surgery_protocol_protocol_id",
                        column: x => x.protocol_id,
                        principalSchema: "clinic",
                        principalTable: "surgery_protocol",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_surgery_protocol_service_id",
                schema: "clinic",
                table: "surgery_protocol",
                column: "service_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_surgery_protocol_follow_up_protocol_id",
                schema: "clinic",
                table: "surgery_protocol_follow_up",
                column: "protocol_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "surgery_protocol_follow_up",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "surgery_protocol",
                schema: "clinic");
        }
    }
}
