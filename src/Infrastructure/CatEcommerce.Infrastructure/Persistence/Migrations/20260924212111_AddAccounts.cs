using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "account");

            migrationBuilder.AddColumn<int>(
                name: "category",
                schema: "audit",
                table: "audit_entry",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "correlation_id",
                schema: "audit",
                table: "audit_entry",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<IPAddress>(
                name: "ip_address",
                schema: "audit",
                table: "audit_entry",
                type: "inet",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_recorded",
                schema: "audit",
                table: "audit_entry",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill pre-existing audit rows to a valid AuditCategory BEFORE the immutability
            // triggers below are installed -- once installed, no UPDATE is possible even for this
            // one-time fix (ARCHITECTURE gate on HO-ACC-001). Every row written before this
            // migration was written by Cat Profiles' three shipped AuditCat*Handlers (HO-CAT-001),
            // which record a change made *because of* a domain event (e.g. a cat flagged high
            // risk) rather than a same-transaction before/after diff -- exactly what HO-ACC-001
            // §8.11 assigns those same three handlers going forward: "The three shipped
            // AuditCat*Handlers are updated (BusinessEvent, IP from IClientIpAccessor)". Backfilling
            // to BusinessEvent (AuditCategory value 3) is therefore the category the handover
            // itself implies for these rows, not a new choice made here. `category = 0` cannot
            // occur for any row written after this migration -- every writer from this point on
            // (the change-capture interceptor, the authentication/business event handlers, the two
            // direct writers in §11.8) sets a real AuditCategory -- so this WHERE clause only ever
            // matches rows that predate the column. `is_recorded` is backfilled to `true` in the
            // same statement for the same reason: every row written since this migration is `true`
            // (ARCHITECTURE gate, second pass, LOW), and once the triggers below exist the value
            // can never be corrected either.
            migrationBuilder.Sql("""
                UPDATE audit.audit_entry SET category = 3, is_recorded = true WHERE category = 0;
                """);

            migrationBuilder.CreateTable(
                name: "customer_account",
                schema: "account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mobile_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    password_algorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    password_set_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    last_failed_login_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    security_stamp = table.Column<Guid>(type: "uuid", nullable: false),
                    registration_channel = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    additional_phone_numbers = table.Column<string[]>(type: "character varying(20)[]", nullable: false),
                    last_successful_login_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    merged_into_customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    merged_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    anonymised_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    last_personal_data_export_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
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
                    table.PrimaryKey("PK_customer_account", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customer_address_book",
                schema: "account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_address_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_customer_address_book", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customer_notification_preferences",
                schema: "account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_customer_notification_preferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customer_session",
                schema: "account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    issued_for_security_stamp = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    expires_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ended_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    end_reason = table.Column<int>(type: "integer", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_customer_session", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_token",
                schema: "account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_kind = table.Column<int>(type: "integer", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    issued_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    expires_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    issued_for_security_stamp = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_password_reset_token", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staff_account",
                schema: "account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mobile_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    password_algorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    password_set_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    last_failed_login_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    security_stamp = table.Column<Guid>(type: "uuid", nullable: false),
                    last_successful_login_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    role_ids = table.Column<string[]>(type: "character varying(50)[]", nullable: false),
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
                    table.PrimaryKey("PK_staff_account", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staff_session",
                schema: "account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    issued_for_security_stamp = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    expires_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    last_activity_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ended_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    end_reason = table.Column<int>(type: "integer", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_staff_session", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customer_delivery_address",
                schema: "account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    District = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DeliveryInstructions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AddedOn = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    LastChangedOn = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_delivery_address", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_delivery_address_customer_address_book_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "account",
                        principalTable: "customer_address_book",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_marketing_consent",
                schema: "account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    is_granted = table.Column<bool>(type: "boolean", nullable: false),
                    recorded_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_marketing_consent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_marketing_consent_customer_notification_preference~",
                        column: x => x.customer_id,
                        principalSchema: "account",
                        principalTable: "customer_notification_preferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entry_correlation_id",
                schema: "audit",
                table: "audit_entry",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entry_EntityType_EntityId",
                schema: "audit",
                table: "audit_entry",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_account_additional_phone_numbers",
                schema: "account",
                table: "customer_account",
                column: "additional_phone_numbers")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_customer_account_email_address",
                schema: "account",
                table: "customer_account",
                column: "email_address",
                unique: true,
                filter: "email_address IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_customer_account_merged_into_customer_id",
                schema: "account",
                table: "customer_account",
                column: "merged_into_customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_account_mobile_number",
                schema: "account",
                table: "customer_account",
                column: "mobile_number",
                unique: true,
                filter: "mobile_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_customer_delivery_address_customer_id",
                schema: "account",
                table: "customer_delivery_address",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_marketing_consent_customer_id",
                schema: "account",
                table: "customer_marketing_consent",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_session_customer_account_id_expires_on",
                schema: "account",
                table: "customer_session",
                columns: new[] { "customer_account_id", "expires_on" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_session_token_hash",
                schema: "account",
                table: "customer_session",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_token_token_hash",
                schema: "account",
                table: "password_reset_token",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_account_email_address",
                schema: "account",
                table: "staff_account",
                column: "email_address",
                unique: true,
                filter: "email_address IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_staff_account_mobile_number",
                schema: "account",
                table: "staff_account",
                column: "mobile_number",
                unique: true,
                filter: "mobile_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_staff_account_role_ids",
                schema: "account",
                table: "staff_account",
                column: "role_ids")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_staff_session_staff_account_id_expires_on",
                schema: "account",
                table: "staff_session",
                columns: new[] { "staff_account_id", "expires_on" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_session_token_hash",
                schema: "account",
                table: "staff_session",
                column: "token_hash",
                unique: true);

            // HO-ACC-001 §11.9 / US-ACC-12 AC2. The database is the authoritative layer of audit
            // immutability -- "anyone" includes someone at a psql prompt. Two triggers on
            // audit.audit_entry: row-level for UPDATE/DELETE, statement-level for TRUNCATE.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION audit.audit_entry_immutable() RETURNS trigger AS $body$
                BEGIN
                    RAISE EXCEPTION 'audit.audit_entry rows are immutable and cannot be updated, deleted or truncated';
                END;
                $body$ LANGUAGE plpgsql;

                CREATE TRIGGER audit_entry_no_update_delete
                    BEFORE UPDATE OR DELETE ON audit.audit_entry
                    FOR EACH ROW EXECUTE FUNCTION audit.audit_entry_immutable();

                CREATE TRIGGER audit_entry_no_truncate
                    BEFORE TRUNCATE ON audit.audit_entry
                    FOR EACH STATEMENT EXECUTE FUNCTION audit.audit_entry_immutable();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS audit_entry_no_update_delete ON audit.audit_entry;
                DROP TRIGGER IF EXISTS audit_entry_no_truncate ON audit.audit_entry;
                DROP FUNCTION IF EXISTS audit.audit_entry_immutable();
                """);

            migrationBuilder.DropTable(
                name: "customer_account",
                schema: "account");

            migrationBuilder.DropTable(
                name: "customer_delivery_address",
                schema: "account");

            migrationBuilder.DropTable(
                name: "customer_marketing_consent",
                schema: "account");

            migrationBuilder.DropTable(
                name: "customer_session",
                schema: "account");

            migrationBuilder.DropTable(
                name: "password_reset_token",
                schema: "account");

            migrationBuilder.DropTable(
                name: "staff_account",
                schema: "account");

            migrationBuilder.DropTable(
                name: "staff_session",
                schema: "account");

            migrationBuilder.DropTable(
                name: "customer_address_book",
                schema: "account");

            migrationBuilder.DropTable(
                name: "customer_notification_preferences",
                schema: "account");

            migrationBuilder.DropIndex(
                name: "IX_audit_entry_correlation_id",
                schema: "audit",
                table: "audit_entry");

            migrationBuilder.DropIndex(
                name: "IX_audit_entry_EntityType_EntityId",
                schema: "audit",
                table: "audit_entry");

            migrationBuilder.DropColumn(
                name: "category",
                schema: "audit",
                table: "audit_entry");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                schema: "audit",
                table: "audit_entry");

            migrationBuilder.DropColumn(
                name: "ip_address",
                schema: "audit",
                table: "audit_entry");

            migrationBuilder.DropColumn(
                name: "is_recorded",
                schema: "audit",
                table: "audit_entry");
        }
    }
}
