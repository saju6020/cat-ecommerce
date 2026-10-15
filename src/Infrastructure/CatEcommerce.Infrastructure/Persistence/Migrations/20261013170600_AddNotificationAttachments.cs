using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "attachment_id",
                schema: "notify",
                table: "notification_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "attachment_kind",
                schema: "notify",
                table: "notification_request",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "attachment_id",
                schema: "notify",
                table: "notification",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "attachment_kind",
                schema: "notify",
                table: "notification",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attachment_id",
                schema: "notify",
                table: "notification_request");

            migrationBuilder.DropColumn(
                name: "attachment_kind",
                schema: "notify",
                table: "notification_request");

            migrationBuilder.DropColumn(
                name: "attachment_id",
                schema: "notify",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "attachment_kind",
                schema: "notify",
                table: "notification");
        }
    }
}
