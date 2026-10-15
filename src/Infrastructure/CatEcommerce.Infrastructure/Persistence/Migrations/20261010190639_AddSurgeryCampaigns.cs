using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSurgeryCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "surgery_campaign",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: false),
                    branch_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    max_cases = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_surgery_campaign", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "surgery_campaign_price",
                schema: "clinic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    price_currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_surgery_campaign_price", x => x.id);
                    table.ForeignKey(
                        name: "FK_surgery_campaign_price_surgery_campaign_campaign_id",
                        column: x => x.campaign_id,
                        principalSchema: "clinic",
                        principalTable: "surgery_campaign",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_surgery_campaign_code",
                schema: "clinic",
                table: "surgery_campaign",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_surgery_campaign_price_campaign_service",
                schema: "clinic",
                table: "surgery_campaign_price",
                columns: new[] { "campaign_id", "service_id" },
                unique: true);

            // HO-SRG-001 section 12.1: the campaign report reads surgeries by campaign and date. A partial index -- most surgeries have no campaign. campaign_id lives in the owned
            // price snapshot on the same row, so the index is hand-written (EF cannot span an owned column and an owner column).
            migrationBuilder.Sql("""
                CREATE INDEX ix_surgery_campaign_starts_on ON clinic.surgery (campaign_id, starts_on) WHERE campaign_id IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS clinic.ix_surgery_campaign_starts_on;");

            migrationBuilder.DropTable(
                name: "surgery_campaign_price",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "surgery_campaign",
                schema: "clinic");
        }
    }
}
