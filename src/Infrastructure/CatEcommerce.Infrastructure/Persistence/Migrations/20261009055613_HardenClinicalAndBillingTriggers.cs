using CatEcommerce.Infrastructure.Billing;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Integrator migration (owner-approved 2026-10-09) closing three trigger gaps recorded in the backlogs. No schema change.
    ///  - docs/backlog/EMR.md row 4: <c>clinic.visit_child_frozen</c> looked only at <c>NEW.visit_id</c>, so a child row could be moved
    ///    off a finalised visit. A child row now never changes <c>visit_id</c> (it is owned by its visit and no path re-parents it), and
    ///    an UPDATE also checks the visit the row already belongs to (<c>OLD.visit_id</c>).
    ///  - docs/backlog/EMR.md row 5: the soft-delete flag (<c>IsMarkedToDelete</c>) hides a record from every read, in effect deleting it
    ///    (ADR-118: no clinical row is ever deleted). It is frozen on <c>visit</c> (open or finalised), <c>consent_record</c> and
    ///    <c>diagnostic_order</c>; <c>diagnostic_order</c> also gets an UPDATE trigger freezing <c>cat_id</c>, <c>visit_id</c>, <c>branch_id</c>.
    ///    No application path sets the flag on these aggregates (only reads filter on it).
    ///  - docs/backlog/BIL.md row 6: every billing table refuses TRUNCATE (<c>P0001</c>). The clinic tables already all have
    ///    <c>BEFORE TRUNCATE</c> triggers (AddClinicalRecordImmutability, AddDiagnostics, AddConsents), so nothing is added there.
    /// </summary>
    /// <inheritdoc />
    public partial class HardenClinicalAndBillingTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var finalised = VisitStatus.Finalised.Value;
            var withdrawn = ConsentStatus.Withdrawn.Value;

            migrationBuilder.Sql($"""
                -- visit: as before, plus the soft-delete flag can never be set (an open visit is not deletable either).
                CREATE OR REPLACE FUNCTION clinic.visit_frozen() RETURNS trigger AS $body$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'clinic.visit rows are never deleted'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    IF OLD.status = {finalised} THEN
                        RAISE EXCEPTION 'clinic.visit is finalised and cannot be changed; add an addendum instead'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    IF NEW."IsMarkedToDelete" IS DISTINCT FROM OLD."IsMarkedToDelete" THEN
                        RAISE EXCEPTION 'clinic.visit cannot be marked for deletion; no clinical row is ever deleted'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    RETURN NEW;
                END;
                $body$ LANGUAGE plpgsql;

                -- visit children: a row never changes visit_id (so it can't be moved on or off a finalised visit).
                CREATE OR REPLACE FUNCTION clinic.visit_child_frozen() RETURNS trigger AS $body$
                DECLARE
                    parent_status integer;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'clinic.% rows are never deleted', TG_TABLE_NAME
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    IF TG_OP = 'UPDATE' THEN
                        IF NEW.visit_id IS DISTINCT FROM OLD.visit_id THEN
                            RAISE EXCEPTION 'clinic.% cannot be moved to another visit', TG_TABLE_NAME
                                USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                        END IF;

                        SELECT status INTO parent_status FROM clinic.visit WHERE "Id" = OLD.visit_id;
                        IF parent_status = {finalised} THEN
                            RAISE EXCEPTION 'clinic.% belongs to a finalised visit and cannot be changed', TG_TABLE_NAME
                                USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                        END IF;
                    END IF;

                    SELECT status INTO parent_status FROM clinic.visit WHERE "Id" = NEW.visit_id;

                    IF parent_status = {finalised} THEN
                        RAISE EXCEPTION 'clinic.% belongs to a finalised visit and cannot be changed', TG_TABLE_NAME
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    RETURN NEW;
                END;
                $body$ LANGUAGE plpgsql;

                -- diagnostic_order: identity and the soft-delete flag never change (status, results etc. still do).
                CREATE OR REPLACE FUNCTION clinic.diagnostic_order_guard() RETURNS trigger AS $body$
                BEGIN
                    IF NEW.cat_id IS DISTINCT FROM OLD.cat_id OR NEW.visit_id IS DISTINCT FROM OLD.visit_id
                       OR NEW.branch_id IS DISTINCT FROM OLD.branch_id THEN
                        RAISE EXCEPTION 'clinic.diagnostic_order identity (cat, visit, branch) cannot be changed'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    IF NEW."IsMarkedToDelete" IS DISTINCT FROM OLD."IsMarkedToDelete" THEN
                        RAISE EXCEPTION 'clinic.diagnostic_order cannot be marked for deletion; no clinical row is ever deleted'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    RETURN NEW;
                END;
                $body$ LANGUAGE plpgsql;

                CREATE TRIGGER tr_diagnostic_order_guard
                    BEFORE UPDATE ON clinic.diagnostic_order
                    FOR EACH ROW EXECUTE FUNCTION clinic.diagnostic_order_guard();

                -- consent_record: the soft-delete flag joins the frozen evidence columns.
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
                       OR NEW.captured_by IS DISTINCT FROM OLD.captured_by OR NEW.witness_staff_id IS DISTINCT FROM OLD.witness_staff_id
                       OR NEW."IsMarkedToDelete" IS DISTINCT FROM OLD."IsMarkedToDelete" THEN
                        RAISE EXCEPTION 'clinic.consent_record evidence cannot be changed; withdraw it and record a new consent'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    RETURN NEW;
                END;
                $body$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(BillingMigrationSql.NoTruncate());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var finalised = VisitStatus.Finalised.Value;
            var withdrawn = ConsentStatus.Withdrawn.Value;

            migrationBuilder.Sql(BillingMigrationSql.DropNoTruncate());

            migrationBuilder.Sql($"""
                DROP TRIGGER IF EXISTS tr_diagnostic_order_guard ON clinic.diagnostic_order;
                DROP FUNCTION IF EXISTS clinic.diagnostic_order_guard();

                CREATE OR REPLACE FUNCTION clinic.visit_frozen() RETURNS trigger AS $body$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'clinic.visit rows are never deleted'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    IF OLD.status = {finalised} THEN
                        RAISE EXCEPTION 'clinic.visit is finalised and cannot be changed; add an addendum instead'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    RETURN NEW;
                END;
                $body$ LANGUAGE plpgsql;

                CREATE OR REPLACE FUNCTION clinic.visit_child_frozen() RETURNS trigger AS $body$
                DECLARE
                    parent_status integer;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'clinic.% rows are never deleted', TG_TABLE_NAME
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    SELECT status INTO parent_status FROM clinic.visit WHERE "Id" = NEW.visit_id;

                    IF parent_status = {finalised} THEN
                        RAISE EXCEPTION 'clinic.% belongs to a finalised visit and cannot be changed', TG_TABLE_NAME
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    RETURN NEW;
                END;
                $body$ LANGUAGE plpgsql;

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
                """);
        }
    }
}
