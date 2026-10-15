using CatEcommerce.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// HO-XCT-001 ADR-299 layer 1: <c>retention.refuse_hard_delete()</c> and, for every Forbidden table, a BEFORE DELETE row trigger
    /// and a BEFORE TRUNCATE statement trigger. <b>The table list below is frozen in this migration</b> (gate fix 2026-10-12, ARCHITECTURE 3):
    /// it is never read from <c>RecordClassification</c> or from another class, so no later edit of a shared list can change what this
    /// migration did. It is the set of <c>Forbidden</c> tables at the time, less <c>audit.audit_entry</c> (HO-ACC-001 F33) and the two
    /// controlled-drug register tables (HO-PHM-001 ADR-144), which carry their own complete guards (<see cref="RetentionGuardSql.DedicatedlyGuardedTables"/>).
    /// DO NOT EDIT: a table classified Forbidden later attaches its triggers in its own migration.
    /// </summary>
    public partial class AddRetentionGuards : Migration
    {
        /// <summary>The tables that received the two triggers in this migration. Read by RetentionGuardMigrationTests (U-A7); never edit.</summary>
        public static readonly IReadOnlyList<(string Schema, string Table)> TablesAtMigration =
        [
            ("account", "customer_account"),
            ("account", "staff_account"),
            ("admin", "branch"),
            ("admin", "branch_closure"),
            ("admin", "business_settings"),
            ("admin", "clinic_service"),
            ("admin", "content_item"),
            ("admin", "leave_request"),
            ("admin", "policy_acceptance"),
            ("admin", "policy_document"),
            ("admin", "policy_version"),
            ("admin", "room"),
            ("admin", "roster_exception"),
            ("admin", "service_price"),
            ("admin", "staff_branch_assignment"),
            ("admin", "staff_roster"),
            ("admin", "system_status"),
            ("admin", "tax_class"),
            ("admin", "tax_rate"),
            ("billing", "accounting_export_run"),
            ("billing", "bank_match"),
            ("billing", "bank_statement"),
            ("billing", "bank_statement_line"),
            ("billing", "billing_profile"),
            ("billing", "cash_up"),
            ("billing", "charge"),
            ("billing", "cod_remittance"),
            ("billing", "cod_remittance_line"),
            ("billing", "credit_note"),
            ("billing", "credit_note_line"),
            ("billing", "document_number_series"),
            ("billing", "invoice"),
            ("billing", "loyalty_account"),
            ("billing", "loyalty_entry"),
            ("billing", "payment"),
            ("billing", "payment_allocation_entry"),
            ("billing", "refund"),
            ("clinic", "appointment"),
            ("clinic", "appointment_status_change"),
            ("clinic", "cat"),
            ("clinic", "cat_parasite_treatment"),
            ("clinic", "cat_vaccination"),
            ("clinic", "cat_weight_record"),
            ("clinic", "consent_record"),
            ("clinic", "consultation_draft"),
            ("clinic", "diagnosis_code"),
            ("clinic", "diagnostic_attachment"),
            ("clinic", "diagnostic_order"),
            ("clinic", "diagnostic_requested_analyte"),
            ("clinic", "diagnostic_result_set"),
            ("clinic", "diagnostic_result_value"),
            ("clinic", "lab_analyte"),
            ("clinic", "note_template"),
            ("clinic", "vaccine"),
            ("clinic", "visit"),
            ("clinic", "visit_addendum"),
            ("clinic", "visit_photo"),
            ("clinic", "visit_problem"),
            ("clinic", "visit_vitals"),
            ("clinic", "waitlist_entry"),
            ("inventory", "batch"),
            ("inventory", "batch_status_change"),
            ("inventory", "consignment"),
            ("inventory", "consignment_document"),
            ("inventory", "consignment_status_change"),
            ("inventory", "goods_receipt"),
            ("inventory", "goods_receipt_line"),
            ("inventory", "purchase_order"),
            ("inventory", "stock_adjustment"),
            ("inventory", "stock_adjustment_line"),
            ("inventory", "stock_alert"),
            ("inventory", "stock_count"),
            ("inventory", "stock_count_line"),
            ("inventory", "stock_ledger_entry"),
            ("inventory", "stock_location"),
            ("inventory", "stock_position"),
            ("inventory", "stock_reservation_entry"),
            ("inventory", "stock_transfer"),
            ("inventory", "stock_transfer_line"),
            ("inventory", "storage_breach_report"),
            ("inventory", "supplier"),
            ("notify", "notification"),
            ("notify", "notification_attempt"),
            ("notify", "notification_request"),
            ("notify", "notification_template"),
            ("ops", "inbound_webhook_receipt"),
            ("ordering", "bank_transfer_account"),
            ("ordering", "cart"),
            ("ordering", "courier"),
            ("ordering", "delivery_zone"),
            ("ordering", "discount_code"),
            ("ordering", "order_line"),
            ("ordering", "order_payment_ref"),
            ("ordering", "order_refund"),
            ("ordering", "order_status_change"),
            ("ordering", "reconciliation_issue"),
            ("ordering", "return_line"),
            ("ordering", "return_request"),
            ("ordering", "sales_order"),
            ("pharmacy", "diet_authorisation"),
            ("pharmacy", "dispensing"),
            ("pharmacy", "dispensing_line"),
            ("pharmacy", "formulary_medicine"),
            ("pharmacy", "prescription"),
            ("pharmacy", "prescription_warning"),
            ("pharmacy", "repeat_request"),
            ("product", "product"),
            ("product", "product_review"),
            ("product", "sku"),
            ("product", "sku_price"),
            ("product", "sourcing_origin"),
            ("product", "stock_alert"),
            ("product", "wishlist"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RetentionGuardSql.CreateFunction);
            foreach (var (schema, table) in TablesAtMigration)
                migrationBuilder.Sql(RetentionGuardSql.AttachTo(schema, table));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (schema, table) in TablesAtMigration)
                migrationBuilder.Sql(RetentionGuardSql.DetachFrom(schema, table));
            migrationBuilder.Sql($"DROP FUNCTION IF EXISTS {RetentionGuardSql.FunctionName}();");
        }
    }
}
