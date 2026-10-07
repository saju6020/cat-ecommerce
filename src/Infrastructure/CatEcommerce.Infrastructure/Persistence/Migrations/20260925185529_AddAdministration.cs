using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "admin");

            migrationBuilder.CreateTable(
                name: "branch",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_line = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("PK_branch", x => x.Id);
                });

            // HO-ADM-001 §8.2 / SRS A-1 -- the one branch the migration seeds: code MAIN, "Main
            // clinic", Asia/Dhaka, no hours, active. Closed until an administrator sets hours.
            migrationBuilder.InsertData(
                schema: "admin",
                table: "branch",
                columns: new[]
                {
                    "Id", "code", "name", "time_zone_id", "address_line", "phone_number", "email", "is_active",
                    "CreatedBy", "CreatedDate", "Language", "LastUpdatedDate", "LastUpdatedBy", "TenantId", "Tags",
                    "VerticalId", "ServiceId", "IsMarkedToDelete", "Version"
                },
                values: new object[]
                {
                    new Guid("00000000-0000-0000-0000-00000000ba1c"), "MAIN", "Main clinic", "Asia/Dhaka", null, null, null, true,
                    Guid.Empty, new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc), Guid.Empty, Guid.Empty, null,
                    Guid.Empty, null, false, 1
                });

            migrationBuilder.CreateTable(
                name: "branch_closure",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    partial_from = table.Column<TimeOnly>(type: "time", nullable: true),
                    partial_to = table.Column<TimeOnly>(type: "time", nullable: true),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_branch_closure", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "business_settings",
                schema: "admin",
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
                    table.PrimaryKey("PK_business_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "clinic_service",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    qualified_role_ids = table.Column<string[]>(type: "text[]", nullable: false),
                    required_room_kind = table.Column<int>(type: "integer", nullable: false),
                    tax_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_bookable_online = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_clinic_service", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "content_item",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    draft_body = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    published_body = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: true),
                    published_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    placement = table.Column<int>(type: "integer", nullable: true),
                    link_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    visible_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    visible_to = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_content_item", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "leave_request",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_leave_request", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "policy_acceptance",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    accepted_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_policy_acceptance", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "policy_document",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    requires_acceptance = table.Column<bool>(type: "boolean", nullable: false),
                    current_version_number = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_policy_document", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "policy_version",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    body = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    published_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    change_summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    requires_acceptance = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_policy_version", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "room",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_room", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roster_exception",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    starts = table.Column<TimeOnly>(type: "time", nullable: false),
                    ends = table.Column<TimeOnly>(type: "time", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_roster_exception", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "service_price",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    set_by = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_service_price", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staff_branch_assignment",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    primary_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_staff_branch_assignment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staff_roster",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    next_pattern_effective_from = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_staff_roster", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_status",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    maintenance_mode = table.Column<bool>(type: "boolean", nullable: false),
                    maintenance_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
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
                    table.PrimaryKey("PK_system_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tax_class",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_tax_class", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tax_rate",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
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
                    table.PrimaryKey("PK_tax_rate", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "branch_opening_interval",
                schema: "admin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    opens = table.Column<TimeOnly>(type: "time", nullable: false),
                    closes = table.Column<TimeOnly>(type: "time", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_opening_interval", x => x.id);
                    table.ForeignKey(
                        name: "FK_branch_opening_interval_branch_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "admin",
                        principalTable: "branch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "business_settings_value",
                schema: "admin",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    business_settings_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_settings_value", x => new { x.business_settings_id, x.key });
                    table.ForeignKey(
                        name: "FK_business_settings_value_business_settings_business_settings~",
                        column: x => x.business_settings_id,
                        principalSchema: "admin",
                        principalTable: "business_settings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_roster_current_shift",
                schema: "admin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    starts = table.Column<TimeOnly>(type: "time", nullable: false),
                    ends = table.Column<TimeOnly>(type: "time", nullable: false),
                    default_room_id = table.Column<Guid>(type: "uuid", nullable: true),
                    staff_roster_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_roster_current_shift", x => x.id);
                    table.ForeignKey(
                        name: "FK_staff_roster_current_shift_staff_roster_staff_roster_id",
                        column: x => x.staff_roster_id,
                        principalSchema: "admin",
                        principalTable: "staff_roster",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_roster_next_shift",
                schema: "admin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    starts = table.Column<TimeOnly>(type: "time", nullable: false),
                    ends = table.Column<TimeOnly>(type: "time", nullable: false),
                    default_room_id = table.Column<Guid>(type: "uuid", nullable: true),
                    staff_roster_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_roster_next_shift", x => x.id);
                    table.ForeignKey(
                        name: "FK_staff_roster_next_shift_staff_roster_staff_roster_id",
                        column: x => x.staff_roster_id,
                        principalSchema: "admin",
                        principalTable: "staff_roster",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_action_occurred",
                schema: "audit",
                table: "audit_entry",
                columns: new[] { "Action", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_actor_occurred",
                schema: "audit",
                table: "audit_entry",
                columns: new[] { "ActorUserId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_entity_type_occurred",
                schema: "audit",
                table: "audit_entry",
                columns: new[] { "EntityType", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_occurred",
                schema: "audit",
                table: "audit_entry",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "ix_branch_code",
                schema: "admin",
                table: "branch",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_branch_closure_branch_range",
                schema: "admin",
                table: "branch_closure",
                columns: new[] { "branch_id", "starts_on", "ends_on" });

            migrationBuilder.CreateIndex(
                name: "IX_branch_opening_interval_branch_id",
                schema: "admin",
                table: "branch_opening_interval",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinic_service_code",
                schema: "admin",
                table: "clinic_service",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_item_kind_slug",
                schema: "admin",
                table: "content_item",
                columns: new[] { "kind", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_leave_request_staff_range",
                schema: "admin",
                table: "leave_request",
                columns: new[] { "staff_account_id", "starts_on", "ends_on" });

            migrationBuilder.CreateIndex(
                name: "ix_leave_request_status",
                schema: "admin",
                table: "leave_request",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_policy_acceptance_customer_key_number",
                schema: "admin",
                table: "policy_acceptance",
                columns: new[] { "customer_account_id", "policy_key", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_policy_document_key",
                schema: "admin",
                table: "policy_document",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_policy_version_key_number",
                schema: "admin",
                table: "policy_version",
                columns: new[] { "policy_key", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_room_branch_name",
                schema: "admin",
                table: "room",
                columns: new[] { "branch_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roster_exception_branch_date",
                schema: "admin",
                table: "roster_exception",
                columns: new[] { "branch_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_roster_exception_staff_date",
                schema: "admin",
                table: "roster_exception",
                columns: new[] { "staff_account_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_service_price_service_effective",
                schema: "admin",
                table: "service_price",
                columns: new[] { "service_id", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_roster_staff_branch",
                schema: "admin",
                table: "staff_roster",
                columns: new[] { "staff_account_id", "branch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_roster_current_shift_staff_roster_id",
                schema: "admin",
                table: "staff_roster_current_shift",
                column: "staff_roster_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_roster_next_shift_staff_roster_id",
                schema: "admin",
                table: "staff_roster_next_shift",
                column: "staff_roster_id");

            migrationBuilder.CreateIndex(
                name: "ix_tax_class_code",
                schema: "admin",
                table: "tax_class",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_rate_class_effective",
                schema: "admin",
                table: "tax_rate",
                columns: new[] { "tax_class_id", "effective_from" },
                unique: true);

            // Gate fix 2026-09-26 (MEDIUM): a handler-level pre-check
            // (RequestLeaveCommandHandler's ExistAsync) cannot stop two concurrent requests from
            // both passing it and both inserting overlapping Requested/Approved leave for the same
            // staff member -- the same class of race UniqueConstraintExceptionFilter's other
            // mappings close for a simple unique column, but no unique index can express "same
            // staff, overlapping date range". A GiST exclusion constraint can, and enforces it at
            // the database, not just in application code. Only Requested (1) and Approved (2) rows
            // participate -- a Rejected or Cancelled leave must never block a new request for the
            // same dates.
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS btree_gist;

                ALTER TABLE admin.leave_request
                ADD CONSTRAINT ex_leave_request_staff_no_overlap
                EXCLUDE USING gist (
                    staff_account_id WITH =,
                    daterange(starts_on, ends_on, '[]') WITH &&
                )
                WHERE (status IN (1, 2));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE admin.leave_request DROP CONSTRAINT IF EXISTS ex_leave_request_staff_no_overlap;
                """);

            migrationBuilder.DropTable(
                name: "branch_closure",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "branch_opening_interval",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "business_settings_value",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "clinic_service",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "content_item",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "leave_request",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "policy_acceptance",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "policy_document",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "policy_version",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "room",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "roster_exception",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "service_price",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "staff_branch_assignment",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "staff_roster_current_shift",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "staff_roster_next_shift",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "system_status",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "tax_class",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "tax_rate",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "branch",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "business_settings",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "staff_roster",
                schema: "admin");

            migrationBuilder.DropIndex(
                name: "ix_audit_entry_action_occurred",
                schema: "audit",
                table: "audit_entry");

            migrationBuilder.DropIndex(
                name: "ix_audit_entry_actor_occurred",
                schema: "audit",
                table: "audit_entry");

            migrationBuilder.DropIndex(
                name: "ix_audit_entry_entity_type_occurred",
                schema: "audit",
                table: "audit_entry");

            migrationBuilder.DropIndex(
                name: "ix_audit_entry_occurred",
                schema: "audit",
                table: "audit_entry");
        }
    }
}
