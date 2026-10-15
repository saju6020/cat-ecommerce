using System;
using CatEcommerce.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardingCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "attention_cleared_by",
                schema: "boarding",
                table: "stay",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attention_cleared_note",
                schema: "boarding",
                table: "stay",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "attention_cleared_on",
                schema: "boarding",
                table: "stay",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "charge_revision",
                schema: "boarding",
                table: "stay",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "credit_due",
                schema: "boarding",
                table: "stay",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "stay_referral",
                schema: "boarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    urgency = table.Column<int>(type: "integer", nullable: false),
                    referred_by = table.Column<Guid>(type: "uuid", nullable: false),
                    referred_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    returned_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stay_referral", x => x.id);
                    table.ForeignKey(
                        name: "FK_stay_referral_stay_stay_id",
                        column: x => x.stay_id,
                        principalSchema: "boarding",
                        principalTable: "stay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stay_surgery",
                schema: "boarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    discharged_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stay_surgery", x => x.id);
                    table.ForeignKey(
                        name: "FK_stay_surgery_stay_stay_id",
                        column: x => x.stay_id,
                        principalSchema: "boarding",
                        principalTable: "stay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stay_vet_approval",
                schema: "boarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referral_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    decided_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_via = table.Column<int>(type: "integer", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stay_vet_approval", x => x.id);
                    table.ForeignKey(
                        name: "FK_stay_vet_approval_stay_stay_id",
                        column: x => x.stay_id,
                        principalSchema: "boarding",
                        principalTable: "stay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stay_vet_commitment",
                schema: "boarding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    recorded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stay_vet_commitment", x => x.id);
                    table.ForeignKey(
                        name: "FK_stay_vet_commitment_stay_stay_id",
                        column: x => x.stay_id,
                        principalSchema: "boarding",
                        principalTable: "stay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stay_referral_stay",
                schema: "boarding",
                table: "stay_referral",
                column: "stay_id");

            migrationBuilder.CreateIndex(
                name: "ix_stay_surgery_stay",
                schema: "boarding",
                table: "stay_surgery",
                column: "stay_id");

            migrationBuilder.CreateIndex(
                name: "ix_stay_vet_approval_stay",
                schema: "boarding",
                table: "stay_vet_approval",
                column: "stay_id");

            migrationBuilder.CreateIndex(
                name: "ix_stay_vet_commitment_charge",
                schema: "boarding",
                table: "stay_vet_commitment",
                column: "charge_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stay_vet_commitment_stay",
                schema: "boarding",
                table: "stay_vet_commitment",
                column: "stay_id");
            // C-ALL-1 / ADR-299 layer 1: every Forbidden table carries the retention trigger pair (U-A7 / I-A3). Literal calls: U-A7 scans for them.
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "stay_referral"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "stay_vet_commitment"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "stay_vet_approval"));
            migrationBuilder.Sql(RetentionGuardSql.AttachTo("boarding", "stay_surgery"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "stay_surgery"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "stay_vet_approval"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "stay_vet_commitment"));
            migrationBuilder.Sql(RetentionGuardSql.DetachFrom("boarding", "stay_referral"));

            migrationBuilder.DropTable(
                name: "stay_referral",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "stay_surgery",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "stay_vet_approval",
                schema: "boarding");

            migrationBuilder.DropTable(
                name: "stay_vet_commitment",
                schema: "boarding");

            migrationBuilder.DropColumn(
                name: "attention_cleared_by",
                schema: "boarding",
                table: "stay");

            migrationBuilder.DropColumn(
                name: "attention_cleared_note",
                schema: "boarding",
                table: "stay");

            migrationBuilder.DropColumn(
                name: "attention_cleared_on",
                schema: "boarding",
                table: "stay");

            migrationBuilder.DropColumn(
                name: "charge_revision",
                schema: "boarding",
                table: "stay");

            migrationBuilder.DropColumn(
                name: "credit_due",
                schema: "boarding",
                table: "stay");
        }
    }
}
