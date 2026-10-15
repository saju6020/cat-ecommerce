namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>
/// HO-ORD-001 §12.1 / V1 -- the hand-written SQL of the Ordering migrations, kept here (not inline in a generated migration) so a test can read the very
/// SQL the migration runs.
///
/// <b>The order-number sequence</b> (<c>ordering.order_number_seq</c>): <c>nextval</c> is increasing and <b>does not roll back</b> when the transaction
/// aborts, so order numbers may have gaps -- an order number is not an invoice number (V1). The handler formats <c>ORD-{yyyy}-{n:D6}</c> and never
/// assumes contiguity.
///
/// <b>Orders are financial records (D-9, D-5, D-6):</b> nothing is deleted. Four tables refuse <c>DELETE</c> and <c>TRUNCATE</c> outright
/// (<c>sales_order</c>, <c>order_line</c>, <c>order_status_change</c>, <c>order_payment_ref</c>), and the two append-only histories
/// (<c>order_status_change</c>, <c>order_payment_ref</c>) also refuse <c>UPDATE</c>. A row trigger does not fire on <c>TRUNCATE</c>, so every table that
/// has row triggers also has a <c>BEFORE TRUNCATE ... FOR EACH STATEMENT</c> trigger. Violations raise <c>P0001</c>: they indicate a code defect and
/// surface as a <c>500</c> with the correlation id, never a mapped business answer. <c>order_line_allocation</c> is deliberately not locked: a staff
/// reallocation before dispatch replaces a line's allocations (US-IMP-07 AC2).
/// </summary>
public static class OrderingMigrationSql
{
    public const string Schema = "ordering";
    public const string OrderNumberSequence = "ordering.order_number_seq";

    /// <summary>Tables that refuse DELETE and TRUNCATE.</summary>
    public static readonly string[] NoDeleteTables = ["sales_order", "order_line", "order_status_change", "order_payment_ref"];

    /// <summary>The append-only histories: they also refuse UPDATE.</summary>
    public static readonly string[] AppendOnlyTables = ["order_status_change", "order_payment_ref"];

    public static string CreateOrderNumberSequence() =>
        "CREATE SEQUENCE IF NOT EXISTS ordering.order_number_seq AS bigint START WITH 1 INCREMENT BY 1 MINVALUE 1 NO CYCLE;";

    public static string DropOrderNumberSequence() => "DROP SEQUENCE IF EXISTS ordering.order_number_seq;";

    public static string ImmutabilityTriggers()
    {
        var sql = new System.Text.StringBuilder();

        sql.AppendLine("""
            CREATE OR REPLACE FUNCTION ordering.fn_orders_never_deleted() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
                RAISE EXCEPTION 'ordering.% rows are never deleted or truncated (D-5, D-6, D-9)', TG_TABLE_NAME USING ERRCODE = 'P0001';
            END
            $fn$;

            CREATE OR REPLACE FUNCTION ordering.fn_orders_append_only() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
                RAISE EXCEPTION 'ordering.% is append-only: rows are never updated (D-5)', TG_TABLE_NAME USING ERRCODE = 'P0001';
            END
            $fn$;
            """);

        foreach (var table in NoDeleteTables)
        {
            sql.AppendLine($"""
                CREATE TRIGGER trg_{table}_no_delete BEFORE DELETE ON ordering.{table}
                    FOR EACH ROW EXECUTE FUNCTION ordering.fn_orders_never_deleted();
                CREATE TRIGGER trg_{table}_no_truncate BEFORE TRUNCATE ON ordering.{table}
                    FOR EACH STATEMENT EXECUTE FUNCTION ordering.fn_orders_never_deleted();
                """);
        }

        foreach (var table in AppendOnlyTables)
        {
            sql.AppendLine($"""
                CREATE TRIGGER trg_{table}_no_update BEFORE UPDATE ON ordering.{table}
                    FOR EACH ROW EXECUTE FUNCTION ordering.fn_orders_append_only();
                """);
        }

        return sql.ToString();
    }

    public static string DropImmutabilityTriggers()
    {
        var sql = new System.Text.StringBuilder();
        foreach (var table in NoDeleteTables)
        {
            sql.AppendLine($"DROP TRIGGER IF EXISTS trg_{table}_no_delete ON ordering.{table};");
            sql.AppendLine($"DROP TRIGGER IF EXISTS trg_{table}_no_truncate ON ordering.{table};");
        }

        foreach (var table in AppendOnlyTables) sql.AppendLine($"DROP TRIGGER IF EXISTS trg_{table}_no_update ON ordering.{table};");

        sql.AppendLine("DROP FUNCTION IF EXISTS ordering.fn_orders_never_deleted();");
        sql.AppendLine("DROP FUNCTION IF EXISTS ordering.fn_orders_append_only();");
        return sql.ToString();
    }

    /// <summary>Phase G: <c>order_refund</c> is append-only (ADR-211, D-5): it refuses UPDATE, DELETE and TRUNCATE. Uses the functions the Phase D migration created.</summary>
    public static string RefundImmutabilityTriggers() => """
        CREATE TRIGGER trg_order_refund_no_update BEFORE UPDATE ON ordering.order_refund
            FOR EACH ROW EXECUTE FUNCTION ordering.fn_orders_append_only();
        CREATE TRIGGER trg_order_refund_no_delete BEFORE DELETE ON ordering.order_refund
            FOR EACH ROW EXECUTE FUNCTION ordering.fn_orders_never_deleted();
        CREATE TRIGGER trg_order_refund_no_truncate BEFORE TRUNCATE ON ordering.order_refund
            FOR EACH STATEMENT EXECUTE FUNCTION ordering.fn_orders_never_deleted();
        """;

    public static string DropRefundImmutabilityTriggers() => """
        DROP TRIGGER IF EXISTS trg_order_refund_no_update ON ordering.order_refund;
        DROP TRIGGER IF EXISTS trg_order_refund_no_delete ON ordering.order_refund;
        DROP TRIGGER IF EXISTS trg_order_refund_no_truncate ON ordering.order_refund;
        """;
}
