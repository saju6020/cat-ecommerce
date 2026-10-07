using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationRequestScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "send_after",
                schema: "notify",
                table: "notification_request",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subject_stamp",
                schema: "notify",
                table: "notification_request",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subject_stamp",
                schema: "notify",
                table: "notification",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "send_after",
                schema: "notify",
                table: "notification_request");

            migrationBuilder.DropColumn(
                name: "subject_stamp",
                schema: "notify",
                table: "notification_request");

            migrationBuilder.DropColumn(
                name: "subject_stamp",
                schema: "notify",
                table: "notification");
        }
    }
}
