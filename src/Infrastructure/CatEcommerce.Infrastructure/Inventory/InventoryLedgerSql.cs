using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>
/// HO-IMP-001 §8.7 / §12.1 -- THE hand-written SQL of the stock ledger: the projection table, the apply triggers, the position guard, the
/// usability trigger, the immutability triggers and the two read views. It lives here, not typed inside the migration files, for one
/// reason: <b>every enumeration value is written FROM the enumeration in C#</b> (<c>MovementKind.Sale.Value</c>,
/// <c>StockLocationKind.Quarantine.Value</c>, <c>BatchStatus.Available.Value</c>), never as a literal, so the SQL cannot drift from the code
/// (U-X2 reads these strings). The expiry predicate is the SQL mirror of <see cref="BatchExpiryRules"/>: <c>today &gt; expires_on</c>, never
/// <c>&gt;=</c> (OQ-20 -- usable through the whole of the expiry date).
///
/// <b>Versioned by use.</b> A migration that has been applied is history and never edited: a later change to a function is a NEW migration
/// that calls <c>CREATE OR REPLACE</c> with its own text, not an edit to a string here that an old migration re-runs on a fresh database.
/// Each member below is the text of the migration named in its summary.
///
/// <b>The upsert shape (FI-1, recorded in HO-IMP-001 §1 Amended).</b> The handover's literal trigger body is a single
/// <c>INSERT ... ON CONFLICT DO UPDATE SET on_hand = on_hand + EXCLUDED.on_hand</c>. Verified on PostgreSQL 17.5 that the <c>CHECK</c> is
/// evaluated against the PROPOSED insert tuple before the conflict is considered, so that shape refuses EVERY deduction with a spurious
/// <c>23514</c>, even one that fits. The equivalent used here is two statements in the same trigger -- insert a zero row <c>ON CONFLICT DO
/// NOTHING</c>, then <c>UPDATE</c> it -- which keeps FI-1's consequence exactly: concurrent movements on one (batch, location) serialise on
/// the row lock and the loser re-reads the committed row and fails <c>ck_stock_position_balance</c> (<c>InventoryDatabaseFactsTests</c>).
/// </summary>
public static class InventoryLedgerSql
{
    public const string ApplySettingName = "inventory.ledger_apply";

    private static string List(IEnumerable<MovementKind> kinds) => string.Join(", ", kinds.Select(k => k.Value));

    // ── AddInventoryStockLedger (Phase D) ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The projection of the ledger (ADR-099): written ONLY by the apply triggers, guarded against everything else.</summary>
    public static string CreateStockPosition() => """
        CREATE TABLE inventory.stock_position (
            batch_id uuid NOT NULL,
            location_id uuid NOT NULL,
            sku_id uuid NOT NULL,
            on_hand numeric(12,3) NOT NULL DEFAULT 0,
            reserved numeric(12,3) NOT NULL DEFAULT 0,
            last_movement_on timestamptz NOT NULL,
            CONSTRAINT pk_stock_position PRIMARY KEY (batch_id, location_id),
            CONSTRAINT fk_stock_position_batch FOREIGN KEY (batch_id) REFERENCES inventory.batch ("Id"),
            CONSTRAINT fk_stock_position_location FOREIGN KEY (location_id) REFERENCES inventory.stock_location ("Id"),
            CONSTRAINT ck_stock_position_balance CHECK (on_hand >= 0 AND reserved >= 0 AND reserved <= on_hand)
        );
        CREATE INDEX ix_stock_position_sku ON inventory.stock_position (sku_id);
        CREATE INDEX ix_stock_position_location ON inventory.stock_position (location_id);
        """;

    /// <summary>The sign of every movement is fixed by its kind, written from <see cref="MovementKindRules"/>.</summary>
    public static string AddLedgerSignCheck() => $"""
        ALTER TABLE inventory.stock_ledger_entry ADD CONSTRAINT ck_stock_ledger_sign CHECK (
            (kind IN ({List(MovementKindRules.Incoming)}) AND quantity_delta > 0)
            OR (kind IN ({List(MovementKindRules.Outgoing)}) AND quantity_delta < 0));
        ALTER TABLE inventory.stock_ledger_entry ADD CONSTRAINT ck_stock_ledger_consumes_reservation CHECK (
            NOT consumes_reservation OR kind = {MovementKind.Sale.Value});
        """;

    /// <summary><c>tg_stock_ledger_apply</c>: <c>on_hand += delta</c>, and when the entry consumes a reservation also <c>reserved += delta</c> --
    /// ONE statement, so the CHECK never sees a half-applied sale (ADR-100).</summary>
    public static string ApplyLedgerFunctionAndTrigger() => $"""
        CREATE FUNCTION inventory.fn_apply_ledger_entry() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            PERFORM set_config('{ApplySettingName}', 'on', true);

            INSERT INTO inventory.stock_position (batch_id, location_id, sku_id, on_hand, reserved, last_movement_on)
            VALUES (NEW.batch_id, NEW.location_id, NEW.sku_id, 0, 0, NEW.occurred_on)
            ON CONFLICT (batch_id, location_id) DO NOTHING;

            UPDATE inventory.stock_position
               SET on_hand = on_hand + NEW.quantity_delta,
                   reserved = reserved + CASE WHEN NEW.consumes_reservation THEN NEW.quantity_delta ELSE 0 END,
                   last_movement_on = GREATEST(last_movement_on, NEW.occurred_on)
             WHERE batch_id = NEW.batch_id AND location_id = NEW.location_id;

            PERFORM set_config('{ApplySettingName}', 'off', true);
            RETURN NEW;
        END
        $fn$;

        CREATE TRIGGER tg_stock_ledger_apply AFTER INSERT ON inventory.stock_ledger_entry
            FOR EACH ROW EXECUTE FUNCTION inventory.fn_apply_ledger_entry();
        """;

    /// <summary><c>tg_stock_position_guard</c>: refuses any write that is not an apply-trigger's own (I-D6).</summary>
    public static string PositionGuardFunctionAndTrigger() => $"""
        CREATE FUNCTION inventory.fn_guard_position_write() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            IF current_setting('{ApplySettingName}', true) IS DISTINCT FROM 'on' THEN
                RAISE EXCEPTION 'inventory.stock_position is maintained only by the ledger triggers and cannot be written directly'
                    USING ERRCODE = '42501', CONSTRAINT = 'tg_stock_position_guard';
            END IF;
            IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
            RETURN NEW;
        END
        $fn$;

        CREATE TRIGGER tg_stock_position_guard BEFORE INSERT OR UPDATE OR DELETE ON inventory.stock_position
            FOR EACH ROW EXECUTE FUNCTION inventory.fn_guard_position_write();
        CREATE TRIGGER tg_stock_position_guard_truncate BEFORE TRUNCATE ON inventory.stock_position
            FOR EACH STATEMENT EXECUTE FUNCTION inventory.fn_guard_position_write();
        """;

    /// <summary>
    /// <c>tg_stock_batch_usable</c> (BEFORE INSERT, both entry tables): for the consuming kinds -- and <c>Reserve</c> on the reservation
    /// table -- reads the batch <c>FOR SHARE</c> (FI-5: a concurrent recall waits for an in-flight consumer, and vice versa) and refuses with
    /// <c>23514</c> naming <c>tg_stock_batch_not_usable</c> when it is not <c>Available</c>, or <c>tg_stock_batch_expired</c> when it is expired
    /// in the LOCATION'S BRANCH-LOCAL date (FI-4). The application checks the same things first so users get specific messages; this exists so
    /// no future consumer can forget (ADR-101). There is no setting, role or parameter that skips it.
    /// </summary>
    public static string BatchUsableFunctionAndLedgerTrigger() => $"""
        CREATE FUNCTION inventory.fn_check_batch_usable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        DECLARE
            batch_status integer;
            batch_expires date;
            zone text;
            branch_today date;
        BEGIN
            IF TG_TABLE_NAME = 'stock_ledger_entry' THEN
                IF NEW.kind NOT IN ({List(MovementKindRules.Consuming)}) THEN RETURN NEW; END IF;
            ELSE
                IF NEW.kind <> {ReservationEntryKind.Reserve.Value} THEN RETURN NEW; END IF;
            END IF;

            SELECT status, expires_on INTO batch_status, batch_expires
              FROM inventory.batch WHERE "Id" = NEW.batch_id FOR SHARE;

            IF NOT FOUND OR batch_status <> {BatchStatus.Available.Value} THEN
                RAISE EXCEPTION 'That batch can no longer be used.'
                    USING ERRCODE = '23514', CONSTRAINT = 'tg_stock_batch_not_usable';
            END IF;

            IF batch_expires IS NOT NULL THEN
                SELECT b.time_zone_id INTO zone
                  FROM inventory.stock_location l JOIN admin.branch b ON b."Id" = l.branch_id
                 WHERE l."Id" = NEW.location_id;

                branch_today := (now() AT TIME ZONE COALESCE(zone, 'UTC'))::date;
                IF branch_today {BatchExpiryRules.SqlComparison} batch_expires THEN
                    RAISE EXCEPTION 'That batch has expired and cannot be sold, dispensed or fed.'
                        USING ERRCODE = '23514', CONSTRAINT = 'tg_stock_batch_expired';
                END IF;
            END IF;

            RETURN NEW;
        END
        $fn$;

        CREATE TRIGGER tg_stock_batch_usable BEFORE INSERT ON inventory.stock_ledger_entry
            FOR EACH ROW EXECUTE FUNCTION inventory.fn_check_batch_usable();
        """;

    /// <summary>The ledger is immutable (ADR-098): UPDATE, DELETE and TRUNCATE are refused -- the HO-ACC-001 audit-trigger pattern.</summary>
    public static string RefuseMutationFunctionAndLedgerTriggers() => """
        CREATE FUNCTION inventory.fn_refuse_mutation() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            RAISE EXCEPTION '% rows are immutable and cannot be updated, deleted or truncated', TG_TABLE_NAME
                USING ERRCODE = '42501';
        END
        $fn$;

        CREATE TRIGGER tg_stock_ledger_immutable BEFORE UPDATE OR DELETE ON inventory.stock_ledger_entry
            FOR EACH ROW EXECUTE FUNCTION inventory.fn_refuse_mutation();
        CREATE TRIGGER tg_stock_ledger_no_truncate BEFORE TRUNCATE ON inventory.stock_ledger_entry
            FOR EACH STATEMENT EXECUTE FUNCTION inventory.fn_refuse_mutation();
        """;

    public static string DropStockLedgerObjects() => """
        DROP TRIGGER IF EXISTS tg_stock_ledger_no_truncate ON inventory.stock_ledger_entry;
        DROP TRIGGER IF EXISTS tg_stock_ledger_immutable ON inventory.stock_ledger_entry;
        DROP TRIGGER IF EXISTS tg_stock_batch_usable ON inventory.stock_ledger_entry;
        DROP TRIGGER IF EXISTS tg_stock_ledger_apply ON inventory.stock_ledger_entry;
        DROP TABLE IF EXISTS inventory.stock_position;
        DROP FUNCTION IF EXISTS inventory.fn_refuse_mutation();
        DROP FUNCTION IF EXISTS inventory.fn_check_batch_usable();
        DROP FUNCTION IF EXISTS inventory.fn_guard_position_write();
        DROP FUNCTION IF EXISTS inventory.fn_apply_ledger_entry();
        """;

    // ── AddInventoryReservations (Phase E) ──────────────────────────────────────────────────────────────────────────────────────

    public static string AddReservationSignCheck() => $"""
        ALTER TABLE inventory.stock_reservation_entry ADD CONSTRAINT ck_stock_reservation_sign CHECK (
            (kind = {ReservationEntryKind.Reserve.Value} AND quantity_delta > 0)
            OR (kind = {ReservationEntryKind.Release.Value} AND quantity_delta < 0));
        """;

    /// <summary><c>tg_stock_reservation_apply</c>: <c>reserved += delta</c> under the same row lock as the ledger trigger, so a reservation and a
    /// deduction of the last free unit serialise and exactly one wins (I-E4, ADR-099).</summary>
    public static string ApplyReservationFunctionAndTrigger() => $"""
        CREATE FUNCTION inventory.fn_apply_reservation_entry() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            PERFORM set_config('{ApplySettingName}', 'on', true);

            INSERT INTO inventory.stock_position (batch_id, location_id, sku_id, on_hand, reserved, last_movement_on)
            VALUES (NEW.batch_id, NEW.location_id, NEW.sku_id, 0, 0, NEW.occurred_on)
            ON CONFLICT (batch_id, location_id) DO NOTHING;

            UPDATE inventory.stock_position
               SET reserved = reserved + NEW.quantity_delta,
                   last_movement_on = GREATEST(last_movement_on, NEW.occurred_on)
             WHERE batch_id = NEW.batch_id AND location_id = NEW.location_id;

            PERFORM set_config('{ApplySettingName}', 'off', true);
            RETURN NEW;
        END
        $fn$;

        CREATE TRIGGER tg_stock_reservation_apply AFTER INSERT ON inventory.stock_reservation_entry
            FOR EACH ROW EXECUTE FUNCTION inventory.fn_apply_reservation_entry();
        """;

    /// <summary>The SAME usability function as the ledger's (it tells the two tables apart by name): a <c>Reserve</c> of an expired, quarantined
    /// or recalled batch is refused by the database too.</summary>
    public static string BatchUsableReservationTrigger() => """
        CREATE TRIGGER tg_stock_batch_usable BEFORE INSERT ON inventory.stock_reservation_entry
            FOR EACH ROW EXECUTE FUNCTION inventory.fn_check_batch_usable();
        """;

    public static string RefuseMutationReservationTriggers() => """
        CREATE TRIGGER tg_stock_reservation_immutable BEFORE UPDATE OR DELETE ON inventory.stock_reservation_entry
            FOR EACH ROW EXECUTE FUNCTION inventory.fn_refuse_mutation();
        CREATE TRIGGER tg_stock_reservation_no_truncate BEFORE TRUNCATE ON inventory.stock_reservation_entry
            FOR EACH STATEMENT EXECUTE FUNCTION inventory.fn_refuse_mutation();
        """;

    /// <summary>
    /// <c>inventory.v_sellable_stock</c> (ADR-116, US-IMP-08 AC1, US-SHP-07): per SKU per branch the quantity SELLABLE at retail -- on-hand minus
    /// reserved over <c>Available</c> batches that are not expired in the branch's local date, at active shop-floor and warehouse locations -- and
    /// the FEFO-first expiry. Quarantined, recalled, written-off and expired stock, and stock at the dispensary, boarding store or quarantine
    /// location, never counts. The expiry test is the SQL mirror of <c>BatchExpiryRules</c>.
    /// </summary>
    public static string CreateSellableStockView() => $"""
        CREATE VIEW inventory.v_sellable_stock AS
        SELECT l.branch_id,
               p.sku_id,
               SUM(p.on_hand - p.reserved) AS sellable_quantity,
               MIN(b.expires_on) FILTER (WHERE p.on_hand - p.reserved > 0) AS next_expires_on
          FROM inventory.stock_position p
          JOIN inventory.batch b ON b."Id" = p.batch_id
          JOIN inventory.stock_location l ON l."Id" = p.location_id
          JOIN admin.branch br ON br."Id" = l.branch_id
         WHERE b.status = {BatchStatus.Available.Value}
           AND l.is_active
           AND l.kind IN ({StockLocationKind.ShopFloor.Value}, {StockLocationKind.Warehouse.Value})
           AND (b.expires_on IS NULL OR NOT ((now() AT TIME ZONE br.time_zone_id)::date {BatchExpiryRules.SqlComparison} b.expires_on))
         GROUP BY l.branch_id, p.sku_id;
        """;

    public static string DropReservationObjects() => """
        DROP VIEW IF EXISTS inventory.v_sellable_stock;
        DROP TRIGGER IF EXISTS tg_stock_reservation_no_truncate ON inventory.stock_reservation_entry;
        DROP TRIGGER IF EXISTS tg_stock_reservation_immutable ON inventory.stock_reservation_entry;
        DROP TRIGGER IF EXISTS tg_stock_batch_usable ON inventory.stock_reservation_entry;
        DROP TRIGGER IF EXISTS tg_stock_reservation_apply ON inventory.stock_reservation_entry;
        DROP FUNCTION IF EXISTS inventory.fn_apply_reservation_entry();
        """;

    /// <summary>
    /// <c>inventory.v_batch_unit_cost</c> (§8.11, US-IMP-05 AC2): per batch the unit cost in base currency -- the LANDED unit cost when the consignment's allocation
    /// has a line for the batch, else the purchase cost -- and which one it is. A batch belongs to at most one consignment, so there is at most one line.
    /// </summary>
    public static string CreateBatchUnitCostView() => """
        CREATE VIEW inventory.v_batch_unit_cost AS
        SELECT b."Id" AS batch_id,
               COALESCE(l.landed_unit_cost_base, b.purchase_unit_cost_base) AS unit_cost_base,
               CASE WHEN l."Id" IS NULL THEN 'Purchase' ELSE 'Landed' END AS cost_basis
          FROM inventory.batch b
          LEFT JOIN inventory.consignment_landed_cost_line l ON l.batch_id = b."Id";
        """;

    public static string DropBatchUnitCostView() => "DROP VIEW IF EXISTS inventory.v_batch_unit_cost;";

    // ── AddSkuSellableSummary (HO-SHP-001-FU2, ADR-116 amendment 2026-10-07) ───────────────────────────────────────────────────

    public const string SummaryApplySettingName = "inventory.summary_apply";

    private static string SellableLocationKinds => $"{StockLocationKind.ShopFloor.Value}, {StockLocationKind.Warehouse.Value}";

    /// <summary>
    /// <c>inventory.sku_sellable_summary</c>: the trigger-kept summary of what <c>v_sellable_stock</c> computes, at the grain
    /// (sku, branch, batch expiry date). A row holds the sellable quantity (on-hand minus reserved) of the SELLABLE-KIND stock
    /// (<c>Available</c> batch, active shop-floor or warehouse location) with that expiry; <c>expires_on = 'infinity'</c> means the
    /// batch has no expiry. The expiry date is part of the key so the time-based part of the rule (usable through the whole of the
    /// expiry date in the branch local date, OQ-20) is applied at READ time by <c>v_sku_sellable</c> -- no write is needed when a
    /// branch midnight passes, so the summary equals the live view at every instant, not "within the worker interval".
    /// Written only by the refresh function (guarded like <c>stock_position</c>).
    /// </summary>
    public static string CreateSkuSellableSummary() => """
        CREATE TABLE inventory.sku_sellable_summary (
            sku_id uuid NOT NULL,
            branch_id uuid NOT NULL,
            expires_on date NOT NULL,
            sellable_quantity numeric(14,3) NOT NULL,
            last_changed_on timestamptz NOT NULL,
            CONSTRAINT pk_sku_sellable_summary PRIMARY KEY (sku_id, branch_id, expires_on),
            CONSTRAINT ck_sku_sellable_summary_quantity CHECK (sellable_quantity >= 0)
        );
        CREATE INDEX ix_sku_sellable_summary_branch ON inventory.sku_sellable_summary (branch_id, sku_id);
        """;

    /// <summary>
    /// <c>fn_refresh_sku_sellable(sku, branch)</c>: recomputes one (sku, branch) from the positions (called only by the commit-time flush) -- delete and re-insert, never an
    /// incremental delta, so no eligibility change (quarantine, recall, write-off, location kind) can leave drift. A transaction-scoped
    /// advisory lock on (sku, branch) serialises concurrent refreshes (taken in (sku, branch) order by the flush, so they cannot deadlock): the second waits for the first to commit and then recomputes from
    /// the committed rows (READ COMMITTED takes a fresh snapshot per statement), so a stale refresh can never overwrite a newer one.
    /// </summary>
    public static string RefreshSkuSellableFunction() => $"""
        CREATE FUNCTION inventory.fn_refresh_sku_sellable(p_sku_id uuid, p_branch_id uuid) RETURNS void LANGUAGE plpgsql AS $fn$
        BEGIN
            PERFORM pg_advisory_xact_lock(hashtextextended(p_sku_id::text || p_branch_id::text, 0));
            PERFORM set_config('{SummaryApplySettingName}', 'on', true);

            DELETE FROM inventory.sku_sellable_summary WHERE sku_id = p_sku_id AND branch_id = p_branch_id;

            INSERT INTO inventory.sku_sellable_summary (sku_id, branch_id, expires_on, sellable_quantity, last_changed_on)
            SELECT p.sku_id, l.branch_id, COALESCE(b.expires_on, 'infinity'::date), SUM(p.on_hand - p.reserved), now()
              FROM inventory.stock_position p
              JOIN inventory.batch b ON b."Id" = p.batch_id
              JOIN inventory.stock_location l ON l."Id" = p.location_id
             WHERE p.sku_id = p_sku_id
               AND l.branch_id = p_branch_id
               AND b.status = {BatchStatus.Available.Value}
               AND l.is_active
               AND l.kind IN ({SellableLocationKinds})
             GROUP BY p.sku_id, l.branch_id, COALESCE(b.expires_on, 'infinity'::date);

            PERFORM set_config('{SummaryApplySettingName}', 'off', true);
        END
        $fn$;
        """;

    /// <summary>The summary is written only by <c>fn_refresh_sku_sellable</c>.</summary>
    public static string SummaryGuardFunctionAndTriggers() => $"""
        CREATE FUNCTION inventory.fn_guard_summary_write() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            IF current_setting('{SummaryApplySettingName}', true) IS DISTINCT FROM 'on' THEN
                RAISE EXCEPTION 'inventory.sku_sellable_summary is maintained only by the stock triggers and cannot be written directly'
                    USING ERRCODE = '42501', CONSTRAINT = 'tg_sku_sellable_summary_guard';
            END IF;
            IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
            RETURN NEW;
        END
        $fn$;

        CREATE TRIGGER tg_sku_sellable_summary_guard BEFORE INSERT OR UPDATE OR DELETE ON inventory.sku_sellable_summary
            FOR EACH ROW EXECUTE FUNCTION inventory.fn_guard_summary_write();
        CREATE TRIGGER tg_sku_sellable_summary_guard_truncate BEFORE TRUNCATE ON inventory.sku_sellable_summary
            FOR EACH STATEMENT EXECUTE FUNCTION inventory.fn_guard_summary_write();
        """;

    /// <summary>
    /// <b>The refresh is deferred to commit and runs in one global lock order.</b> The maintenance triggers do not refresh; they MARK the touched (sku, branch)
    /// in <c>sku_sellable_dirty</c> (keyed by transaction). A <c>DEFERRABLE INITIALLY DEFERRED</c> constraint trigger on that table fires at commit and, on its
    /// first firing, refreshes every marked pair of the transaction ordered by (sku_id, branch_id), then clears them; later firings find nothing. So however
    /// many SKUs a unit of work touched, and in whatever order its writers did, every transaction takes its advisory locks in the same order and only after all
    /// its row locks -- a lock cycle on the summary is impossible (review HIGH, fix round 1). The summary is therefore stale INSIDE the writing transaction;
    /// nothing reads it there (Shop reads it in other requests, Inventory reads positions and <c>v_sellable_stock</c>).
    /// </summary>
    public static string SummaryDeferredRefresh() => """
        CREATE TABLE inventory.sku_sellable_dirty (
            txid bigint NOT NULL,
            sku_id uuid NOT NULL,
            branch_id uuid NOT NULL,
            CONSTRAINT pk_sku_sellable_dirty PRIMARY KEY (txid, sku_id, branch_id)
        );

        CREATE FUNCTION inventory.fn_mark_sku_sellable_dirty(p_sku_id uuid, p_branch_id uuid) RETURNS void LANGUAGE plpgsql AS $fn$
        BEGIN
            INSERT INTO inventory.sku_sellable_dirty (txid, sku_id, branch_id) VALUES (txid_current(), p_sku_id, p_branch_id) ON CONFLICT DO NOTHING;
        END
        $fn$;

        CREATE FUNCTION inventory.fn_flush_sku_sellable_dirty() RETURNS trigger LANGUAGE plpgsql AS $fn$
        DECLARE
            pair record;
        BEGIN
            FOR pair IN SELECT sku_id, branch_id FROM inventory.sku_sellable_dirty WHERE txid = txid_current() ORDER BY sku_id, branch_id LOOP
                PERFORM inventory.fn_refresh_sku_sellable(pair.sku_id, pair.branch_id);
            END LOOP;
            DELETE FROM inventory.sku_sellable_dirty WHERE txid = txid_current();
            RETURN NULL;
        END
        $fn$;

        CREATE CONSTRAINT TRIGGER tg_flush_sku_sellable_dirty AFTER INSERT ON inventory.sku_sellable_dirty
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.fn_flush_sku_sellable_dirty();
        """;

    /// <summary>
    /// The three triggers that keep the summary equal to the view, all in the writer transaction (they mark; the commit-time flush refreshes): (1) <c>stock_position</c> -- the SAME
    /// rows the ledger and reservation apply triggers write, so on-hand and reserved changes (receipt, sale, transfer, adjustment,
    /// reservation, release) are covered; (2) <c>batch</c> -- status (quarantine, release, recall, write-off) and expiry date changes;
    /// (3) <c>stock_location</c> -- active flag, kind and branch. A branch time zone is not stored, so it needs no trigger.
    /// </summary>
    public static string SummaryMaintenanceTriggers() => """
        CREATE FUNCTION inventory.fn_summary_after_position() RETURNS trigger LANGUAGE plpgsql AS $fn$
        DECLARE
            v_branch uuid;
        BEGIN
            IF TG_OP = 'UPDATE' AND NEW.on_hand = OLD.on_hand AND NEW.reserved = OLD.reserved THEN RETURN NULL; END IF;
            SELECT branch_id INTO v_branch FROM inventory.stock_location WHERE "Id" = COALESCE(NEW.location_id, OLD.location_id);
            PERFORM inventory.fn_mark_sku_sellable_dirty(COALESCE(NEW.sku_id, OLD.sku_id), v_branch);
            RETURN NULL;
        END
        $fn$;

        CREATE TRIGGER tg_summary_after_position AFTER INSERT OR UPDATE OR DELETE ON inventory.stock_position
            FOR EACH ROW EXECUTE FUNCTION inventory.fn_summary_after_position();

        CREATE FUNCTION inventory.fn_summary_after_batch() RETURNS trigger LANGUAGE plpgsql AS $fn$
        DECLARE
            v_branch uuid;
        BEGIN
            FOR v_branch IN
                SELECT DISTINCT l.branch_id FROM inventory.stock_position p
                  JOIN inventory.stock_location l ON l."Id" = p.location_id
                 WHERE p.batch_id = NEW."Id"
            LOOP
                PERFORM inventory.fn_mark_sku_sellable_dirty(NEW.sku_id, v_branch);
            END LOOP;
            RETURN NULL;
        END
        $fn$;

        CREATE TRIGGER tg_summary_after_batch AFTER UPDATE ON inventory.batch
            FOR EACH ROW WHEN (OLD.status IS DISTINCT FROM NEW.status OR OLD.expires_on IS DISTINCT FROM NEW.expires_on)
            EXECUTE FUNCTION inventory.fn_summary_after_batch();

        CREATE FUNCTION inventory.fn_summary_after_location() RETURNS trigger LANGUAGE plpgsql AS $fn$
        DECLARE
            v_sku uuid;
        BEGIN
            FOR v_sku IN SELECT DISTINCT sku_id FROM inventory.stock_position WHERE location_id = NEW."Id" LOOP
                PERFORM inventory.fn_mark_sku_sellable_dirty(v_sku, OLD.branch_id);
                IF NEW.branch_id <> OLD.branch_id THEN PERFORM inventory.fn_mark_sku_sellable_dirty(v_sku, NEW.branch_id); END IF;
            END LOOP;
            RETURN NULL;
        END
        $fn$;

        CREATE TRIGGER tg_summary_after_location AFTER UPDATE ON inventory.stock_location
            FOR EACH ROW WHEN (OLD.is_active IS DISTINCT FROM NEW.is_active OR OLD.kind IS DISTINCT FROM NEW.kind OR OLD.branch_id IS DISTINCT FROM NEW.branch_id)
            EXECUTE FUNCTION inventory.fn_summary_after_location();
        """;

    /// <summary>Fills the summary from the positions that exist when the migration runs (the refresh function query, for every sku and branch).</summary>
    public static string BackfillSkuSellableSummary() => $"""
        SELECT set_config('{SummaryApplySettingName}', 'on', true);
        INSERT INTO inventory.sku_sellable_summary (sku_id, branch_id, expires_on, sellable_quantity, last_changed_on)
        SELECT p.sku_id, l.branch_id, COALESCE(b.expires_on, 'infinity'::date), SUM(p.on_hand - p.reserved), now()
          FROM inventory.stock_position p
          JOIN inventory.batch b ON b."Id" = p.batch_id
          JOIN inventory.stock_location l ON l."Id" = p.location_id
         WHERE b.status = {BatchStatus.Available.Value}
           AND l.is_active
           AND l.kind IN ({SellableLocationKinds})
         GROUP BY p.sku_id, l.branch_id, COALESCE(b.expires_on, 'infinity'::date);
        SELECT set_config('{SummaryApplySettingName}', 'off', true);
        """;

    /// <summary>
    /// <c>inventory.v_sku_sellable</c> (the new published read contract beside <c>v_sellable_stock</c>): per (sku, branch, expiry date) the sellable
    /// quantity of stock that is not expired in the branch local date -- the expiry test is the SQL mirror of <c>BatchExpiryRules</c>, applied
    /// here at read time. <c>expires_on</c> is NULL for undated stock. A consumer reads rows with <c>sellable_quantity &gt; 0</c> directly (no
    /// aggregate) or sums by (sku, branch) to get exactly <c>v_sellable_stock.sellable_quantity</c>; the earliest dated bucket with quantity
    /// is its <c>next_expires_on</c>.
    /// </summary>
    public static string CreateSkuSellableView() => $"""
        CREATE VIEW inventory.v_sku_sellable AS
        SELECT s.branch_id,
               s.sku_id,
               NULLIF(s.expires_on, 'infinity'::date) AS expires_on,
               s.sellable_quantity,
               s.last_changed_on
          FROM inventory.sku_sellable_summary s
          JOIN admin.branch br ON br."Id" = s.branch_id
         WHERE s.expires_on = 'infinity'::date
            OR NOT ((now() AT TIME ZONE br.time_zone_id)::date {BatchExpiryRules.SqlComparison} s.expires_on);
        """;

    public static string DropSkuSellableSummaryObjects() => """
        DROP VIEW IF EXISTS inventory.v_sku_sellable;
        DROP TRIGGER IF EXISTS tg_flush_sku_sellable_dirty ON inventory.sku_sellable_dirty;
        DROP TRIGGER IF EXISTS tg_summary_after_location ON inventory.stock_location;
        DROP TRIGGER IF EXISTS tg_summary_after_batch ON inventory.batch;
        DROP TRIGGER IF EXISTS tg_summary_after_position ON inventory.stock_position;
        DROP TABLE IF EXISTS inventory.sku_sellable_summary;
        DROP TABLE IF EXISTS inventory.sku_sellable_dirty;
        DROP FUNCTION IF EXISTS inventory.fn_flush_sku_sellable_dirty();
        DROP FUNCTION IF EXISTS inventory.fn_mark_sku_sellable_dirty(uuid, uuid);
        DROP FUNCTION IF EXISTS inventory.fn_summary_after_location();
        DROP FUNCTION IF EXISTS inventory.fn_summary_after_batch();
        DROP FUNCTION IF EXISTS inventory.fn_summary_after_position();
        DROP FUNCTION IF EXISTS inventory.fn_guard_summary_write();
        DROP FUNCTION IF EXISTS inventory.fn_refresh_sku_sellable(uuid, uuid);
        """;
}
