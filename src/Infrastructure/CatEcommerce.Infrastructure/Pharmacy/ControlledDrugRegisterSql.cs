using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Infrastructure.Pharmacy;

/// <summary>
/// HO-PHM-001 §8.9 / §12.1 (ADR-144, V2) -- THE hand-written SQL of the controlled-drug register: the sequence its entries are numbered from and the four
/// triggers that refuse UPDATE, DELETE and TRUNCATE on <c>pharmacy.cd_register_entry</c> and <c>pharmacy.cd_register_seal</c>. It lives here, not typed inside the
/// migration, so a test can read the same text the migration ran. The refusal is a plain <c>RAISE EXCEPTION</c> with its own SQLSTATE (<c>PH001</c>) --
/// deliberately NOT mapped to an HTTP answer: reaching it is a programming error and a logged 500 is correct (the immutability interceptor should have
/// stopped it first).
///
/// <b>The migration's Down drops these triggers and the tables and must NEVER be run against a database holding real entries</b> (HO-PHM-001 §20.8). Nothing in the
/// code base -- not a data fix, not a merge -- may disable a trigger or UPDATE or DELETE a register row (ADR-144).
/// </summary>
public static class ControlledDrugRegisterSql
{
    public const string RefusalSqlState = "PH001";
    public const string EntryNumberSequence = "pharmacy.cd_register_entry_number_seq";

    public static string CreateEntryNumberSequence() => $"CREATE SEQUENCE {EntryNumberSequence} START WITH 1 INCREMENT BY 1 NO CYCLE;";

    /// <summary>The row function (UPDATE/DELETE), the statement function (TRUNCATE) and the four triggers on the two tables.</summary>
    public static string CreateImmutabilityTriggers() => $"""
        CREATE FUNCTION pharmacy.fn_cd_register_row_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            RAISE EXCEPTION '{PharmacyMessages.RegisterAppendOnly}: % on %.% is refused', TG_OP, TG_TABLE_SCHEMA, TG_TABLE_NAME
                USING ERRCODE = '{RefusalSqlState}', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
        END
        $fn$;

        CREATE FUNCTION pharmacy.fn_cd_register_truncate_refused() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            RAISE EXCEPTION '{PharmacyMessages.RegisterAppendOnly}: TRUNCATE of %.% is refused', TG_TABLE_SCHEMA, TG_TABLE_NAME
                USING ERRCODE = '{RefusalSqlState}', SCHEMA = TG_TABLE_SCHEMA, TABLE = TG_TABLE_NAME;
        END
        $fn$;

        CREATE TRIGGER tg_cd_register_entry_immutable BEFORE UPDATE OR DELETE ON pharmacy.cd_register_entry
            FOR EACH ROW EXECUTE FUNCTION pharmacy.fn_cd_register_row_immutable();
        CREATE TRIGGER tg_cd_register_entry_no_truncate BEFORE TRUNCATE ON pharmacy.cd_register_entry
            FOR EACH STATEMENT EXECUTE FUNCTION pharmacy.fn_cd_register_truncate_refused();
        CREATE TRIGGER tg_cd_register_seal_immutable BEFORE UPDATE OR DELETE ON pharmacy.cd_register_seal
            FOR EACH ROW EXECUTE FUNCTION pharmacy.fn_cd_register_row_immutable();
        CREATE TRIGGER tg_cd_register_seal_no_truncate BEFORE TRUNCATE ON pharmacy.cd_register_seal
            FOR EACH STATEMENT EXECUTE FUNCTION pharmacy.fn_cd_register_truncate_refused();
        """;

    public static string DropImmutabilityTriggers() => """
        DROP TRIGGER IF EXISTS tg_cd_register_seal_no_truncate ON pharmacy.cd_register_seal;
        DROP TRIGGER IF EXISTS tg_cd_register_seal_immutable ON pharmacy.cd_register_seal;
        DROP TRIGGER IF EXISTS tg_cd_register_entry_no_truncate ON pharmacy.cd_register_entry;
        DROP TRIGGER IF EXISTS tg_cd_register_entry_immutable ON pharmacy.cd_register_entry;
        DROP FUNCTION IF EXISTS pharmacy.fn_cd_register_truncate_refused();
        DROP FUNCTION IF EXISTS pharmacy.fn_cd_register_row_immutable();
        """;
}
