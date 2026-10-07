using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notify");

            migrationBuilder.AddColumn<int>(
                name: "preferred_channel",
                schema: "account",
                table: "customer_notification_preferences",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notification",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    template_version = table.Column<int>(type: "integer", nullable: false),
                    locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    urgency = table.Column<int>(type: "integer", nullable: false),
                    contains_secret = table.Column<bool>(type: "boolean", nullable: false),
                    recipient_kind = table.Column<int>(type: "integer", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_kind = table.Column<int>(type: "integer", nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    destination = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    subject = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    html_body = table.Column<string>(type: "text", nullable: true),
                    link_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    not_before = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    deferred_for_quiet_hours = table.Column<bool>(type: "boolean", nullable: false),
                    not_after = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    next_attempt_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    lease_expires_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    fallback_from_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resend_of_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resent_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resend_ordinal = table.Column<int>(type: "integer", nullable: true),
                    unsubscribe_token_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    unsubscribe_link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    read_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    sent_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    closed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    close_reason = table.Column<int>(type: "integer", nullable: true),
                    redacted_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    redacted_reason = table.Column<int>(type: "integer", nullable: true),
                    anonymised_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
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
                    table.PrimaryKey("PK_notification", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_request",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    recipient_kind = table.Column<int>(type: "integer", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variables = table.Column<string>(type: "jsonb", nullable: false),
                    channel_hint = table.Column<int>(type: "integer", nullable: true),
                    subject_kind = table.Column<int>(type: "integer", nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deduplication_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    not_after = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    contains_secret = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    suppression_reason = table.Column<int>(type: "integer", nullable: true),
                    requested_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    processed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variables_redacted_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_attempt_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
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
                    table.PrimaryKey("PK_notification_request", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_template",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    content_version = table.Column<int>(type: "integer", nullable: false),
                    last_edited_by = table.Column<Guid>(type: "uuid", nullable: false),
                    last_edited_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
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
                    table.PrimaryKey("PK_notification_template", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_attempt",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempted_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    outcome = table.Column<int>(type: "integer", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    provider_response = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_attempt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_attempt_notification_notification_id",
                        column: x => x.notification_id,
                        principalSchema: "notify",
                        principalTable: "notification",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_template_content",
                schema: "notify",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    subject = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    link_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_template_content", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_template_content_notification_template_templat~",
                        column: x => x.template_id,
                        principalSchema: "notify",
                        principalTable: "notification_template",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_created",
                schema: "notify",
                table: "notification",
                column: "created_on");

            migrationBuilder.CreateIndex(
                name: "ix_notification_destination_created",
                schema: "notify",
                table: "notification",
                columns: new[] { "destination", "created_on" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_fallback_from_id",
                schema: "notify",
                table: "notification",
                column: "fallback_from_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_inbox",
                schema: "notify",
                table: "notification",
                columns: new[] { "recipient_id", "channel", "read_on", "created_on" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_recipient_created",
                schema: "notify",
                table: "notification",
                columns: new[] { "recipient_id", "created_on" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_request_id",
                schema: "notify",
                table: "notification",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_resend_of_id",
                schema: "notify",
                table: "notification",
                column: "resend_of_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_resend_of_id_ordinal",
                schema: "notify",
                table: "notification",
                columns: new[] { "resend_of_id", "resend_ordinal" },
                unique: true,
                filter: "resend_of_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notification_status_notbefore_nextattempt",
                schema: "notify",
                table: "notification",
                columns: new[] { "status", "not_before", "next_attempt_on" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_templatekey_created",
                schema: "notify",
                table: "notification",
                columns: new[] { "template_key", "created_on" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_unsubscribe_token_hash",
                schema: "notify",
                table: "notification",
                column: "unsubscribe_token_hash",
                unique: true,
                filter: "unsubscribe_token_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notification_attempt_notification_id",
                schema: "notify",
                table: "notification_attempt",
                column: "notification_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_request_dedup",
                schema: "notify",
                table: "notification_request",
                column: "deduplication_key",
                unique: true,
                filter: "deduplication_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notification_request_status_requested_on",
                schema: "notify",
                table: "notification_request",
                columns: new[] { "status", "requested_on" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_template_key",
                schema: "notify",
                table: "notification_template",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_template_content_template_channel",
                schema: "notify",
                table: "notification_template_content",
                columns: new[] { "template_id", "channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_attempt",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "notification_request",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "notification_template_content",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "notification",
                schema: "notify");

            migrationBuilder.DropTable(
                name: "notification_template",
                schema: "notify");

            migrationBuilder.DropColumn(
                name: "preferred_channel",
                schema: "account",
                table: "customer_notification_preferences");
        }
    }
}
