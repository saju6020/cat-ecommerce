using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// HO-EMR-001 §8.4 / ADR-118 / BR-11 / D-5 -- the database is the authoritative third layer of clinical
    /// immutability ("anyone" includes someone at a psql prompt). The aggregate refuses first
    /// (<c>VisitAggregate.EnsureOpen</c>) and the API has no path; these triggers are what is left if either is
    /// bypassed, and they raise <c>SQLSTATE CR001</c>, which <c>ClinicalRecordFrozenFilter</c> maps to a logged
    /// <c>409</c>. This migration creates the reusable trigger functions and attaches them to the tables that
    /// exist at this point (visit, its children, addenda); <c>AddDiagnostics</c> and <c>AddConsents</c> attach the
    /// same functions to their own tables.
    ///
    /// The finalised status value comes from the enumeration, never a literal (U-X3).
    /// </summary>
    /// <inheritdoc />
    public partial class AddClinicalRecordImmutability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var finalised = VisitStatus.Finalised.Value;

            migrationBuilder.Sql($"""
                -- clinic.visit: a finalised visit never changes; no visit is ever deleted.
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

                -- The visit's owned children (problems, vitals, photos): no INSERT or UPDATE once the parent visit is
                -- finalised, and no DELETE ever (a problem removed from an open visit is withdrawn, not deleted --
                -- C-ALL-4; this is stricter than the handover's tr_no_delete row, which allowed open-visit problem deletes).
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

                -- Append-only tables (addenda, result sets and values, attachments): INSERT only.
                CREATE OR REPLACE FUNCTION clinic.append_only() RETURNS trigger AS $body$
                BEGIN
                    RAISE EXCEPTION 'clinic.% is append-only: rows are never updated or deleted', TG_TABLE_NAME
                        USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                END;
                $body$ LANGUAGE plpgsql;

                -- Clinical records that are corrected, never removed (diagnostic orders, consents).
                CREATE OR REPLACE FUNCTION clinic.no_delete() RETURNS trigger AS $body$
                BEGIN
                    RAISE EXCEPTION 'clinic.% rows are never deleted', TG_TABLE_NAME
                        USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                END;
                $body$ LANGUAGE plpgsql;

                -- TRUNCATE is refused on every protected table.
                CREATE OR REPLACE FUNCTION clinic.no_truncate() RETURNS trigger AS $body$
                BEGIN
                    RAISE EXCEPTION 'clinic.% cannot be truncated', TG_TABLE_NAME
                        USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                END;
                $body$ LANGUAGE plpgsql;

                CREATE TRIGGER tr_visit_frozen
                    BEFORE UPDATE OR DELETE ON clinic.visit
                    FOR EACH ROW EXECUTE FUNCTION clinic.visit_frozen();

                CREATE TRIGGER tr_visit_problem_frozen
                    BEFORE INSERT OR UPDATE OR DELETE ON clinic.visit_problem
                    FOR EACH ROW EXECUTE FUNCTION clinic.visit_child_frozen();
                CREATE TRIGGER tr_visit_vitals_frozen
                    BEFORE INSERT OR UPDATE OR DELETE ON clinic.visit_vitals
                    FOR EACH ROW EXECUTE FUNCTION clinic.visit_child_frozen();
                CREATE TRIGGER tr_visit_photo_frozen
                    BEFORE INSERT OR UPDATE OR DELETE ON clinic.visit_photo
                    FOR EACH ROW EXECUTE FUNCTION clinic.visit_child_frozen();

                CREATE TRIGGER tr_visit_addendum_append_only
                    BEFORE UPDATE OR DELETE ON clinic.visit_addendum
                    FOR EACH ROW EXECUTE FUNCTION clinic.append_only();

                CREATE TRIGGER tr_visit_no_truncate BEFORE TRUNCATE ON clinic.visit
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.no_truncate();
                CREATE TRIGGER tr_visit_problem_no_truncate BEFORE TRUNCATE ON clinic.visit_problem
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.no_truncate();
                CREATE TRIGGER tr_visit_vitals_no_truncate BEFORE TRUNCATE ON clinic.visit_vitals
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.no_truncate();
                CREATE TRIGGER tr_visit_photo_no_truncate BEFORE TRUNCATE ON clinic.visit_photo
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.no_truncate();
                CREATE TRIGGER tr_visit_addendum_no_truncate BEFORE TRUNCATE ON clinic.visit_addendum
                    FOR EACH STATEMENT EXECUTE FUNCTION clinic.no_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS tr_visit_addendum_no_truncate ON clinic.visit_addendum;
                DROP TRIGGER IF EXISTS tr_visit_photo_no_truncate ON clinic.visit_photo;
                DROP TRIGGER IF EXISTS tr_visit_vitals_no_truncate ON clinic.visit_vitals;
                DROP TRIGGER IF EXISTS tr_visit_problem_no_truncate ON clinic.visit_problem;
                DROP TRIGGER IF EXISTS tr_visit_no_truncate ON clinic.visit;
                DROP TRIGGER IF EXISTS tr_visit_addendum_append_only ON clinic.visit_addendum;
                DROP TRIGGER IF EXISTS tr_visit_photo_frozen ON clinic.visit_photo;
                DROP TRIGGER IF EXISTS tr_visit_vitals_frozen ON clinic.visit_vitals;
                DROP TRIGGER IF EXISTS tr_visit_problem_frozen ON clinic.visit_problem;
                DROP TRIGGER IF EXISTS tr_visit_frozen ON clinic.visit;
                DROP FUNCTION IF EXISTS clinic.no_truncate();
                DROP FUNCTION IF EXISTS clinic.no_delete();
                DROP FUNCTION IF EXISTS clinic.append_only();
                DROP FUNCTION IF EXISTS clinic.visit_child_frozen();
                DROP FUNCTION IF EXISTS clinic.visit_frozen();
                """);
        }
    }
}
