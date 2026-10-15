using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderingEdgeInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.CreateTable(
                name: "idempotency_record",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "char(64)", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: true),
                    response_content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    response_body = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    completed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_record", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbound_webhook_receipt",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    event_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    received_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_webhook_receipt", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_record_created_on",
                schema: "ops",
                table: "idempotency_record",
                column: "created_on");

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_record_scope_key",
                schema: "ops",
                table: "idempotency_record",
                columns: new[] { "scope", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inbound_webhook_receipt_provider_event",
                schema: "ops",
                table: "inbound_webhook_receipt",
                columns: new[] { "provider", "event_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_record",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "inbound_webhook_receipt",
                schema: "ops");
        }
    }
}
