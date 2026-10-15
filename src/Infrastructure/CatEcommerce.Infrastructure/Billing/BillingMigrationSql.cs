using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>
/// HO-BIL-001 §8.6 / §12.1 (ADR-180) -- the hand-written SQL of the Billing migrations: the <b>immutability triggers</b>.
/// Kept here, not inline in a generated migration, so a unit test can read the very SQL the migration runs (U-X3): status
/// values are always written from <c>Enumeration.Value</c> -- never typed as literals -- and every Billing table refuses
/// <c>DELETE</c> except the lines of a <i>draft</i> invoice (D-6).
///
/// The base-class bookkeeping columns (<c>Version</c>, <c>LastUpdatedDate</c>, <c>LastUpdatedBy</c>) and <c>xmin</c> are
/// the only columns an otherwise-immutable row may still change -- they move on every save. Violations raise
/// <c>P0001</c>; the application never maps them: they indicate a code defect and surface as a <c>500</c> with the
/// correlation id (§12.5).
/// </summary>
public static class BillingMigrationSql
{
    public static readonly string[] BookkeepingColumns = ["Version", "LastUpdatedDate", "LastUpdatedBy"];

    // ── Invoice and its lines ───────────────────────────────────────────────────────────────────────
    public static string InvoiceImmutability()
    {
        var draft = InvoiceStatus.Draft.Value;
        var discarded = InvoiceStatus.Discarded.Value;
        var issuedStates = string.Join(", ", new[] { InvoiceStatus.Issued, InvoiceStatus.PartPaid, InvoiceStatus.Paid, InvoiceStatus.Credited }.Select(s => s.Value));
        var mutable = ColumnList("paid_total", "credited_total", "status", "Version", "LastUpdatedDate", "LastUpdatedBy");

        return $$"""
            CREATE OR REPLACE FUNCTION billing.fn_invoice_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'billing.invoice rows are never deleted (BR-11, D-5, D-6)' USING ERRCODE = 'P0001';
                END IF;

                IF OLD.status = {{draft}} THEN
                    RETURN NEW; -- a draft is editable; issuing it is the transition out of draft
                END IF;

                IF OLD.status = {{discarded}} THEN
                    RAISE EXCEPTION 'A discarded draft invoice cannot change (ADR-180)' USING ERRCODE = 'P0001';
                END IF;

                IF NEW.status NOT IN ({{issuedStates}}) THEN
                    RAISE EXCEPTION 'An issued invoice cannot go back to a draft or be discarded (ADR-180)' USING ERRCODE = 'P0001';
                END IF;

                IF (to_jsonb(NEW) - {{mutable}}) IS DISTINCT FROM (to_jsonb(OLD) - {{mutable}}) THEN
                    RAISE EXCEPTION 'An issued invoice is immutable: only its paid and credited totals and its status move (BR-11, ADR-180)' USING ERRCODE = 'P0001';
                END IF;

                RETURN NEW;
            END
            $fn$;

            CREATE TRIGGER trg_invoice_immutable BEFORE UPDATE OR DELETE ON billing.invoice
                FOR EACH ROW EXECUTE FUNCTION billing.fn_invoice_immutable();

            CREATE OR REPLACE FUNCTION billing.fn_invoice_line_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
            DECLARE
                parent_status integer;
            BEGIN
                IF TG_OP = 'INSERT' THEN
                    SELECT status INTO parent_status FROM billing.invoice WHERE "Id" = NEW.invoice_id;
                    IF parent_status = {{draft}} THEN
                        RETURN NEW;
                    END IF;

                    -- Issue-from-charges creates the invoice and its (already frozen) lines in ONE save: the parent row
                    -- was inserted by this very transaction, so its xmin is this transaction's id.
                    IF EXISTS (SELECT 1 FROM billing.invoice WHERE "Id" = NEW.invoice_id
                               AND xmin::text = (pg_current_xact_id()::text::bigint % 4294967296)::text) THEN
                        RETURN NEW;
                    END IF;

                    RAISE EXCEPTION 'Lines cannot be added to an invoice that is not a draft (BR-11, ADR-180)' USING ERRCODE = 'P0001';
                END IF;

                IF TG_OP = 'UPDATE' THEN
                    IF OLD.is_frozen THEN
                        RAISE EXCEPTION 'An issued invoice line is immutable (BR-11, ADR-180)' USING ERRCODE = 'P0001';
                    END IF;
                    RETURN NEW; -- a draft line, including the single freeze that happens at issue
                END IF;

                SELECT status INTO parent_status FROM billing.invoice WHERE "Id" = OLD.invoice_id;
                IF parent_status = {{draft}} AND NOT OLD.is_frozen THEN
                    RETURN OLD;
                END IF;

                RAISE EXCEPTION 'Only the lines of a draft invoice can be removed (BR-11, D-6)' USING ERRCODE = 'P0001';
            END
            $fn$;

            CREATE TRIGGER trg_invoice_line_immutable BEFORE INSERT OR UPDATE OR DELETE ON billing.invoice_line
                FOR EACH ROW EXECUTE FUNCTION billing.fn_invoice_line_immutable();
            """;
    }

    public static string DropInvoiceImmutability() => """
        DROP TRIGGER IF EXISTS trg_invoice_line_immutable ON billing.invoice_line;
        DROP FUNCTION IF EXISTS billing.fn_invoice_line_immutable();
        DROP TRIGGER IF EXISTS trg_invoice_immutable ON billing.invoice;
        DROP FUNCTION IF EXISTS billing.fn_invoice_immutable();
        """;

    // ── Append-only tables (credit notes, refunds, allocation entries, statement lines, ...) ─────────
    /// <summary>
    /// <c>DELETE</c> always raises; <c>UPDATE</c> raises when any column other than the bookkeeping columns and
    /// <paramref name="mutableColumns"/> changes. <paramref name="table"/> and the trigger name come from code, never
    /// from input.
    /// </summary>
    public static string AppendOnly(string table, params string[] mutableColumns)
    {
        var mutable = ColumnList([.. BookkeepingColumns, .. mutableColumns]);

        return $$"""
            CREATE OR REPLACE FUNCTION billing.fn_{{table}}_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'billing.{{table}} rows are never deleted (D-5, D-6)' USING ERRCODE = 'P0001';
                END IF;

                IF (to_jsonb(NEW) - {{mutable}}) IS DISTINCT FROM (to_jsonb(OLD) - {{mutable}}) THEN
                    RAISE EXCEPTION 'billing.{{table}} is append-only: its recorded facts cannot change (D-5, ADR-180)' USING ERRCODE = 'P0001';
                END IF;

                RETURN NEW;
            END
            $fn$;

            CREATE TRIGGER trg_{{table}}_immutable BEFORE UPDATE OR DELETE ON billing.{{table}}
                FOR EACH ROW EXECUTE FUNCTION billing.fn_{{table}}_immutable();
            """;
    }

    public static string DropAppendOnly(string table) => $"""
        DROP TRIGGER IF EXISTS trg_{table}_immutable ON billing.{table};
        DROP FUNCTION IF EXISTS billing.fn_{table}_immutable();
        """;

    /// <summary>A table whose rows may change (a payment's status, a cash-up's count) but are never deleted.</summary>
    public static string NoDelete(string table) => $$"""
        CREATE OR REPLACE FUNCTION billing.fn_{{table}}_nodelete() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            RAISE EXCEPTION 'billing.{{table}} rows are never deleted (D-5, D-6)' USING ERRCODE = 'P0001';
        END
        $fn$;

        CREATE TRIGGER trg_{{table}}_nodelete BEFORE DELETE ON billing.{{table}}
            FOR EACH ROW EXECUTE FUNCTION billing.fn_{{table}}_nodelete();
        """;

    public static string DropNoDelete(string table) => $"""
        DROP TRIGGER IF EXISTS trg_{table}_nodelete ON billing.{table};
        DROP FUNCTION IF EXISTS billing.fn_{table}_nodelete();
        """;

    /// <summary>A cash-up changes while <c>Open</c> (the count) and is terminal once <c>Closed</c> (ADR-188); it is never deleted.</summary>
    public static string ClosedCashUpImmutability() => $$"""
        CREATE OR REPLACE FUNCTION billing.fn_cash_up_closed_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            IF TG_OP = 'DELETE' THEN
                RAISE EXCEPTION 'billing.cash_up rows are never deleted (D-5, D-6)' USING ERRCODE = 'P0001';
            END IF;

            IF OLD.status = {{CashUpStatus.Closed.Value}} THEN
                RAISE EXCEPTION 'a closed cash-up is terminal and cannot change (ADR-188)' USING ERRCODE = 'P0001';
            END IF;

            RETURN NEW;
        END
        $fn$;

        CREATE TRIGGER trg_cash_up_closed_immutable BEFORE UPDATE OR DELETE ON billing.cash_up
            FOR EACH ROW EXECUTE FUNCTION billing.fn_cash_up_closed_immutable();
        """;

    public static string DropClosedCashUpImmutability() => """
        DROP TRIGGER IF EXISTS trg_cash_up_closed_immutable ON billing.cash_up;
        DROP FUNCTION IF EXISTS billing.fn_cash_up_closed_immutable();
        """;

    /// <summary>
    /// A payment's <b>core</b> columns can never change (US-BIL-11 AC3: reversal and re-entry, never edit); its status, the
    /// approval, reversal and refund fields, the earmark release and the cached totals can. <c>DELETE</c> always raises.
    /// </summary>
    public static string PaymentImmutability()
    {
        var mutable = ColumnList(
            [.. BookkeepingColumns, "status", "approved_by", "approved_on", "rejected_by", "rejected_on", "rejection_reason", "accepted_on",
                "business_date", "reversed_by", "reversed_on", "reversal_reason", "reversal_business_date", "refunded_total",
                "earmark_released_on", "earmark_released_by", "earmark_release_reason"]);

        return $$"""
            CREATE OR REPLACE FUNCTION billing.fn_payment_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'billing.payment rows are never deleted (D-5, D-6)' USING ERRCODE = 'P0001';
                END IF;

                IF (to_jsonb(NEW) - {{mutable}}) IS DISTINCT FROM (to_jsonb(OLD) - {{mutable}}) THEN
                    RAISE EXCEPTION 'A payment amount, method, reference, received date, collector and recorder cannot change: reverse it and record it again (US-BIL-11, ADR-183)' USING ERRCODE = 'P0001';
                END IF;

                RETURN NEW;
            END
            $fn$;

            CREATE TRIGGER trg_payment_immutable BEFORE UPDATE OR DELETE ON billing.payment
                FOR EACH ROW EXECUTE FUNCTION billing.fn_payment_immutable();
            """;
    }

    public static string DropPaymentImmutability() => DropAppendOnly("payment");

    private static string ColumnList(params string[] columns) => "ARRAY[" + string.Join(", ", columns.Select(c => "'" + c + "'")) + "]";

    // ── TRUNCATE ────────────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// <c>TRUNCATE</c> never fires a row trigger, so every other guard in this class is bypassed by it. This installs one shared
    /// <c>BEFORE TRUNCATE ... FOR EACH STATEMENT</c> trigger on <b>every</b> table that exists in the billing schema when it runs
    /// (all 21 -- including the two AggregateOnly working sets, which the aggregates replace row by row, never by truncating).
    /// Raises <c>P0001</c> like the rest of the schema (HO-BIL-001 backlog row 6, TEST gate XI-7).
    /// </summary>
    public static string NoTruncate() => $$"""
        CREATE OR REPLACE FUNCTION billing.fn_no_truncate() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            RAISE EXCEPTION 'billing.% cannot be truncated (D-5, D-6)', TG_TABLE_NAME USING ERRCODE = 'P0001';
        END
        $fn$;

        DO $do$
        DECLARE t text;
        BEGIN
            FOR t IN SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                     WHERE n.nspname = 'billing' AND c.relkind IN ('r', 'p') ORDER BY 1
            LOOP
                EXECUTE format('DROP TRIGGER IF EXISTS trg_no_truncate ON billing.%I', t);
                EXECUTE format('CREATE TRIGGER trg_no_truncate BEFORE TRUNCATE ON billing.%I FOR EACH STATEMENT EXECUTE FUNCTION billing.fn_no_truncate()', t);
            END LOOP;
        END
        $do$;
        """;

    public static string DropNoTruncate() => """
        DO $do$
        DECLARE t text;
        BEGIN
            FOR t IN SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                     WHERE n.nspname = 'billing' AND c.relkind IN ('r', 'p') ORDER BY 1
            LOOP
                EXECUTE format('DROP TRIGGER IF EXISTS trg_no_truncate ON billing.%I', t);
            END LOOP;
        END
        $do$;
        DROP FUNCTION IF EXISTS billing.fn_no_truncate();
        """;
}
