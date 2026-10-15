using Microsoft.EntityFrameworkCore.Migrations;

namespace CatEcommerce.Infrastructure.Retention;

/// <summary>
/// HO-XCT-001 section 12.1 / ADR-299 layer 1. The SQL building blocks of the retention triggers: the function, and for one table a <c>BEFORE DELETE</c>
/// row trigger plus a <c>BEFORE TRUNCATE</c> statement trigger (a TRUNCATE does not fire row triggers, F36); every trigger raises <c>P0001</c>
/// "Hard delete of schema.table is not permitted". The table list of migration <c>AddRetentionGuards</c> is <b>frozen inside that migration</b>, not here
/// (gate fix 2026-10-12): a table classified <c>Forbidden</c> later attaches the two triggers in its own migration (C-ALL-1) using <see cref="AttachTo"/>;
/// <c>RetentionGuardMigrationTests</c> (U-A7) and the database test I-A3 prove no Forbidden table is left uncovered.
/// </summary>
public static class RetentionGuardSql
{
    public const string FunctionName = "retention.refuse_hard_delete";
    public const string RowTrigger = "trg_refuse_hard_delete";
    public const string TruncateTrigger = "trg_refuse_truncate";

    /// <summary>
    /// Forbidden tables that already carry a complete append-only guard of their own (BEFORE DELETE row trigger and BEFORE TRUNCATE statement trigger), so a
    /// second pair would only duplicate it. <c>audit.audit_entry</c>: HO-ACC-001 (F33). The two controlled-drug register tables: HO-PHM-001 ADR-144
    /// (<c>PharmacyRegisterEndpointTests.I_D1</c> pins their trigger count at four).
    /// </summary>
    public static readonly IReadOnlyList<(string Schema, string Table, string Reason)> DedicatedlyGuardedTables =
    [
        ("audit", "audit_entry", "HO-ACC-001 F33: BEFORE UPDATE OR DELETE and BEFORE TRUNCATE triggers"),
        ("pharmacy", "cd_register_entry", "HO-PHM-001 ADR-144: append-only DELETE and TRUNCATE triggers"),
        ("pharmacy", "cd_register_seal", "HO-PHM-001 ADR-144: append-only DELETE and TRUNCATE triggers"),
    ];


    public const string CreateFunction = """
        CREATE SCHEMA IF NOT EXISTS retention;
        CREATE OR REPLACE FUNCTION retention.refuse_hard_delete() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          RAISE EXCEPTION 'Hard delete of %.% is not permitted: retained record (C-3, D-6)',
                          TG_TABLE_SCHEMA, TG_TABLE_NAME USING ERRCODE = 'P0001';
        END $$;
        """;

    /// <summary>The two triggers for one table. Later migrations for newly added Forbidden tables call this too.</summary>
    public static string AttachTo(string schema, string table) => $"""
        CREATE TRIGGER {RowTrigger} BEFORE DELETE ON {schema}.{table}
          FOR EACH ROW EXECUTE FUNCTION {FunctionName}();
        CREATE TRIGGER {TruncateTrigger} BEFORE TRUNCATE ON {schema}.{table}
          FOR EACH STATEMENT EXECUTE FUNCTION {FunctionName}();
        """;

    public static string DetachFrom(string schema, string table) => $"""
        DROP TRIGGER IF EXISTS {RowTrigger} ON {schema}.{table};
        DROP TRIGGER IF EXISTS {TruncateTrigger} ON {schema}.{table};
        """;
}
