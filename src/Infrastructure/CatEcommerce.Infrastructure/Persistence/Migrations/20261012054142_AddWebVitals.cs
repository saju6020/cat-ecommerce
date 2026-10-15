using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebVitals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "web_vital_batch",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    build_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_web_vital_batch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "web_vital_sample",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    route_template = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    navigation_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    effective_connection_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    viewport_class = table.Column<int>(type: "integer", nullable: false),
                    web_vital_batch_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_web_vital_sample", x => x.Id);
                    table.ForeignKey(
                        name: "FK_web_vital_sample_web_vital_batch_web_vital_batch_id",
                        column: x => x.web_vital_batch_id,
                        principalSchema: "ops",
                        principalTable: "web_vital_batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_web_vital_batch_received_on",
                schema: "ops",
                table: "web_vital_batch",
                column: "received_on");

            migrationBuilder.CreateIndex(
                name: "ix_web_vital_sample_route_metric_viewport",
                schema: "ops",
                table: "web_vital_sample",
                columns: new[] { "route_template", "metric", "viewport_class" });

            migrationBuilder.CreateIndex(
                name: "IX_web_vital_sample_web_vital_batch_id",
                schema: "ops",
                table: "web_vital_sample",
                column: "web_vital_batch_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "web_vital_sample",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "web_vital_batch",
                schema: "ops");
        }
    }
}
