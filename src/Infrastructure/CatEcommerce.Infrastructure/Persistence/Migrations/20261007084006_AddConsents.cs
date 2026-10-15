using System;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consent_record",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    procedure_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    signature = table.Column<string>(type: "jsonb", nullable: true),
                    file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    signer_relationship = table.Column<int>(type: "integer", nullable: false),
                    signed_by_customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    policy_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    captured_by = table.Column<Guid>(type: "uuid", nullable: false),
                    witness_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    withdrawn_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    withdrawn_by = table.Column<Guid>(type: "uuid", nullable: true),
                    withdrawal_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_consent_record", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consent_record_cat_appointment_kind_status",
                schema: "clinic",
                table: "consent_record",
                columns: new[] { "cat_id", "appointment_id", "kind", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_consent_record_file_id",
                schema: "clinic",
                table: "consent_record",
                column: "file_id",
                unique: true,
                filter: "file_id IS NOT NULL");

            // HO-EMR-001 §8.4 / ADR-118 / NFR-LEG-4 -- a signed consent is a legal record: never deleted, never truncated, and
            // its evidence never edited. The only permitted UPDATE is the one that withdraws it (status, withdrawn_on/by/reason,
            // and the aggregate's own version/audit columns); after that it changes no more. The functions append_only /
            // no_delete / no_truncate were created by AddClinicalRecordImmutability.
            var withdrawn = ConsentStatus.Withdrawn.Value;

            migrationBuilder.Sql($"""
                CREATE OR REPLACE FUNCTION clinic.consent_record_guard() RETURNS trigger AS $body$
                BEGIN
                    IF OLD.status = {withdrawn} THEN
                        RAISE EXCEPTION 'clinic.consent_record is withdrawn and cannot be changed'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    IF NEW.cat_id IS DISTINCT FROM OLD.cat_id OR NEW.appointment_id IS DISTINCT FROM OLD.appointment_id
                       OR NEW.branch_id IS DISTINCT FROM OLD.branch_id OR NEW.kind IS DISTINCT FROM OLD.kind
                       OR NEW.procedure_description IS DISTINCT FROM OLD.procedure_description OR NEW.method IS DISTINCT FROM OLD.method
                       OR NEW.signature IS DISTINCT FROM OLD.signature OR NEW.file_id IS DISTINCT FROM OLD.file_id
                       OR NEW.signed_by_name IS DISTINCT FROM OLD.signed_by_name OR NEW.signer_relationship IS DISTINCT FROM OLD.signer_relationship
                       OR NEW.signed_by_customer_id IS DISTINCT FROM OLD.signed_by_customer_id OR NEW.signed_on IS DISTINCT FROM OLD.signed_on
                       OR NEW.policy_key IS DISTINCT FROM OLD.policy_key OR NEW.policy_version_number IS DISTINCT FROM OLD.policy_version_number
                       OR NEW.captured_by IS DISTINCT FROM OLD.captured_by OR NEW.witness_staff_id IS DISTINCT FROM OLD.witness_staff_id THEN
                        RAISE EXCEPTION 'clinic.consent_record evidence cannot be changed; withdraw it and record a new consent'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    RETURN NEW;
                END;
                $body$ LANGUAGE plpgsql;

                CREATE TRIGGER tr_consent_record_guard
                    BEFORE UPDATE ON clinic.consent_record
                    FOR EACH ROW EXECUTE FUNCTION clinic.consent_record_guard();
                CREATE TRIGGER tr_consent_record_no_delete
                    BEFORE DELETE ON clinic.consent_record
                    FOR EACH ROW EXECUTE FUNCTION clinic.no_delete();
                CREATE TRIGGER tr_consent_record_no_truncate BEFORE TRUNCATE ON clinic.consent_record
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.no_truncate();

                -- A stored file that a consent references cannot be deleted either (replaces the function AddDiagnostics created).
                CREATE OR REPLACE FUNCTION clinic.stored_file_referenced() RETURNS trigger AS $body$
                BEGIN
                    IF EXISTS (SELECT 1 FROM clinic.visit_photo WHERE file_id = OLD."Id")
                       OR EXISTS (SELECT 1 FROM clinic.diagnostic_attachment WHERE file_id = OLD."Id")
                       OR EXISTS (SELECT 1 FROM clinic.consent_record WHERE file_id = OLD."Id") THEN
                        RAISE EXCEPTION 'files.stored_file is referenced by a clinical record and cannot be deleted'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    RETURN OLD;
                END;
                $body$ LANGUAGE plpgsql;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION clinic.stored_file_referenced() RETURNS trigger AS $body$
                BEGIN
                    IF EXISTS (SELECT 1 FROM clinic.visit_photo WHERE file_id = OLD."Id")
                       OR EXISTS (SELECT 1 FROM clinic.diagnostic_attachment WHERE file_id = OLD."Id") THEN
                        RAISE EXCEPTION 'files.stored_file is referenced by a clinical record and cannot be deleted'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    RETURN OLD;
                END;
                $body$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS tr_consent_record_no_truncate ON clinic.consent_record;
                DROP TRIGGER IF EXISTS tr_consent_record_no_delete ON clinic.consent_record;
                DROP TRIGGER IF EXISTS tr_consent_record_guard ON clinic.consent_record;
                DROP FUNCTION IF EXISTS clinic.consent_record_guard();
                """);

            migrationBuilder.DropTable(
                name: "consent_record",
                schema: "clinic");
        }
    }
}
