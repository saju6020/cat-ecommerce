using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitBillingFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                schema: "clinic",
                table: "visit",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_at_discharge_id",
                schema: "clinic",
                table: "surgery",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tax_class_id",
                schema: "clinic",
                table: "diagnostic_order",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_price_amount",
                schema: "clinic",
                table: "diagnostic_order",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unit_price_currency_code",
                schema: "clinic",
                table: "diagnostic_order",
                type: "char(3)",
                nullable: true);
            // HO-BIL-001 Phase J (SECURITY/ARCHITECTURE gates): the price and tax class an order was made with are frozen in the database too (ADR-345).
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION clinic.diagnostic_order_guard() RETURNS trigger AS $body$
                BEGIN
                    IF NEW.cat_id IS DISTINCT FROM OLD.cat_id OR NEW.visit_id IS DISTINCT FROM OLD.visit_id
                       OR NEW.branch_id IS DISTINCT FROM OLD.branch_id THEN
                        RAISE EXCEPTION 'clinic.diagnostic_order identity (cat, visit, branch) cannot be changed'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    IF NEW.unit_price_amount IS DISTINCT FROM OLD.unit_price_amount
                       OR NEW.unit_price_currency_code IS DISTINCT FROM OLD.unit_price_currency_code
                       OR NEW.tax_class_id IS DISTINCT FROM OLD.tax_class_id THEN
                        RAISE EXCEPTION 'clinic.diagnostic_order price and tax class are frozen when the order is made'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    IF NEW."IsMarkedToDelete" IS DISTINCT FROM OLD."IsMarkedToDelete" THEN
                        RAISE EXCEPTION 'clinic.diagnostic_order cannot be marked for deletion; no clinical row is ever deleted'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    RETURN NEW;
                END;
                $body$ LANGUAGE plpgsql;
                """);

            // HO-BIL-001 Phase J (re-check R3): the owner a surgery was discharged under is the customer billed; once set it never changes. A NEW trigger -- the existing
            // retention guards and every other trigger on clinic.surgery are left exactly as they are.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION clinic.surgery_owner_at_discharge_frozen() RETURNS trigger AS $body$
                BEGIN
                    IF OLD.owner_at_discharge_id IS NOT NULL AND NEW.owner_at_discharge_id IS DISTINCT FROM OLD.owner_at_discharge_id THEN
                        RAISE EXCEPTION 'clinic.surgery.owner_at_discharge_id is the customer billed and cannot be changed once set'
                            USING ERRCODE = 'CR001', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
                    END IF;

                    RETURN NEW;
                END;
                $body$ LANGUAGE plpgsql;

                CREATE TRIGGER tr_surgery_owner_at_discharge_frozen
                    BEFORE UPDATE OF owner_at_discharge_id ON clinic.surgery
                    FOR EACH ROW EXECUTE FUNCTION clinic.surgery_owner_at_discharge_frozen();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS tr_surgery_owner_at_discharge_frozen ON clinic.surgery;
                DROP FUNCTION IF EXISTS clinic.surgery_owner_at_discharge_frozen();
                """);
            migrationBuilder.DropColumn(
                name: "customer_id",
                schema: "clinic",
                table: "visit");

            migrationBuilder.DropColumn(
                name: "owner_at_discharge_id",
                schema: "clinic",
                table: "surgery");

            migrationBuilder.DropColumn(
                name: "tax_class_id",
                schema: "clinic",
                table: "diagnostic_order");

            migrationBuilder.DropColumn(
                name: "unit_price_amount",
                schema: "clinic",
                table: "diagnostic_order");

            migrationBuilder.DropColumn(
                name: "unit_price_currency_code",
                schema: "clinic",
                table: "diagnostic_order");
            migrationBuilder.Sql("""
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
                """);
        }
    }
}
