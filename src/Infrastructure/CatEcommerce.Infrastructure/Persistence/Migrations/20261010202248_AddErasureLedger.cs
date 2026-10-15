using System;
using CatEcommerce.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddErasureLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "erasure_request",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    completed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
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
                    table.PrimaryKey("pk_erasure_request", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "erasure_scope",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    context_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    held_until = table.Column<DateOnly>(type: "date", nullable: true),
                    held_basis = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    completed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    erasure_request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_erasure_scope", x => x.Id);
                    table.ForeignKey(
                        name: "FK_erasure_scope_erasure_request_erasure_request_id",
                        column: x => x.erasure_request_id,
                        principalSchema: "ops",
                        principalTable: "erasure_request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_erasure_request_status_requested_on",
                schema: "ops",
                table: "erasure_request",
                columns: new[] { "status", "requested_on" });

            migrationBuilder.CreateIndex(
                name: "ix_erasure_scope_request_context",
                schema: "ops",
                table: "erasure_scope",
                columns: new[] { "erasure_request_id", "context_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_erasure_scope_status_held_until",
                schema: "ops",
                table: "erasure_scope",
                columns: new[] { "status", "held_until" });

            // C-ALL-1: both ops tables are Regulatory and Forbidden, so they get the two retention triggers in the migration that creates them.
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("ops", "erasure_request"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("ops", "erasure_scope"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("ops", "erasure_scope"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("ops", "erasure_request"));

            migrationBuilder.DropTable(
                name: "erasure_scope",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "erasure_request",
                schema: "ops");
        }
    }
}
