using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Persistence;

/// <summary>
/// HO-BIL-001 §16 I-A1: every migration applied in order, from empty, produces the whole <c>billing</c> schema --
/// every table, every named index, every immutability trigger and the receipt sequence. Runs against a real,
/// disposable scratch database on the same server as <c>CATECOMMERCE_TEST_DB</c> (never that database itself),
/// the pattern <see cref="MigrationsOnAnEmptyDatabaseTests"/> established.
/// </summary>
public sealed class BillingSchemaMigrationTests : IAsyncLifetime
{
    private readonly string _databaseName = $"catecommerce_billing_schema_test_{Guid.NewGuid():N}";

    private static NpgsqlConnectionStringBuilder Server()
    {
        var configured = TestDatabase.ConnectionString;
        return new NpgsqlConnectionStringBuilder(configured);
    }

    private string AdminConnectionString => new NpgsqlConnectionStringBuilder(Server().ConnectionString) { Database = "postgres", Pooling = false }.ConnectionString;

    private string ScratchConnectionString => new NpgsqlConnectionStringBuilder(Server().ConnectionString) { Database = _databaseName, Pooling = false }.ConnectionString;

    public async Task InitializeAsync()
    {
        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
        await create.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();
        await using (var terminate = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid()", admin))
        {
            terminate.Parameters.AddWithValue("name", _databaseName);
            await terminate.ExecuteNonQueryAsync();
        }

        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\"", admin);
        await drop.ExecuteNonQueryAsync();
    }

    public static readonly string[] ExpectedTables =
    [
        "billing_profile", "billing_profile_bank_account", "document_number_series", "charge", "invoice", "invoice_line",
        "payment", "payment_allocation_entry", "payment_requested_allocation",
        "credit_note", "credit_note_line", "refund", "cash_up",
        "cod_remittance", "cod_remittance_line", "bank_statement", "bank_statement_line", "bank_match",
        "accounting_export_run", "loyalty_account", "loyalty_entry",
    ];

    public static readonly string[] ExpectedIndexes =
    [
        "ix_document_number_series", "ix_charge_source", "ix_charge_customer_status", "ix_charge_billing_group", "ix_charge_branch_service_date",
        "ix_invoice_number", "ix_invoice_sequence", "ix_invoice_customer_status", "ix_invoice_branch_issue_date", "ix_invoice_status_due_on",
        "ix_invoice_idempotency", "ix_invoice_line_charge",
        "ix_payment_idempotency", "ix_payment_customer_status", "ix_payment_branch_business_date", "ix_payment_method_status_received_on", "ix_payment_earmark",
        "ix_payment_status_recorded_on", "ix_payment_allocation_entry_invoice",
        "ix_credit_note_number", "ix_credit_note_sequence", "ix_credit_note_invoice", "ix_credit_note_idempotency", "ix_refund_idempotency", "ix_refund_payment", "ix_refund_branch_business_date", "ix_cash_up_branch_date",
        "ix_bank_statement_source", "ix_bank_match_line", "ix_bank_match_payment",
        "ix_accounting_export_run_run_on",
        "ix_loyalty_entry_kind_invoice", "ix_loyalty_entry_kind_credit_note", "ix_loyalty_entry_kind_source_customer",
    ];

    public static readonly string[] ExpectedTriggers =
    [
        "trg_invoice_immutable", "trg_invoice_line_immutable", "trg_billing_profile_nodelete", "trg_document_number_series_nodelete", "trg_charge_nodelete",
        "trg_payment_immutable", "trg_payment_allocation_entry_immutable",
        "trg_credit_note_immutable", "trg_credit_note_line_immutable", "trg_refund_immutable", "trg_cash_up_closed_immutable",
        "trg_cod_remittance_immutable", "trg_cod_remittance_line_immutable", "trg_bank_statement_immutable", "trg_bank_statement_line_immutable", "trg_bank_match_immutable",
        "trg_accounting_export_run_immutable",
        "trg_loyalty_entry_immutable", "trg_loyalty_account_nodelete",
    ];

    [Fact]
    public async Task I_A1_EveryMigration_FromEmpty_ProducesTheBillingSchema()
    {
        var options = new DbContextOptionsBuilder<CatEcommerceDbContext>().UseNpgsql(ScratchConnectionString).Options;
        await using (var db = new CatEcommerceDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(ScratchConnectionString);
        await connection.OpenAsync();

        var tables = await ColumnAsync(connection, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'billing'");
        foreach (var table in ExpectedTables) Assert.Contains(table, tables);

        var indexes = await ColumnAsync(connection, "SELECT indexname FROM pg_indexes WHERE schemaname = 'billing'");
        foreach (var index in ExpectedIndexes) Assert.Contains(index, indexes);

        var triggers = await ColumnAsync(connection,
            "SELECT t.tgname FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'billing' AND NOT t.tgisinternal");
        foreach (var trigger in ExpectedTriggers) Assert.Contains(trigger, triggers);

        var sequences = await ColumnAsync(connection, "SELECT sequencename FROM pg_sequences WHERE schemaname = 'billing'");
        Assert.Contains("receipt_number_seq", sequences);

        // D-6: no Billing record is ever deleted. Every table classified Forbidden carries a BEFORE DELETE trigger (tgtype bit 8)
        // -- except invoice_line, whose trigger decides row by row (only a draft's lines may go).
        var deleteGuarded = await ColumnAsync(connection,
            "SELECT c.relname FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace " +
            "WHERE n.nspname = 'billing' AND NOT t.tgisinternal AND (t.tgtype & 8) = 8");

        foreach (var table in tables.Where(t => t != "invoice_line" && RecordClassification.Find("billing", t)?.HardDeletePolicy == HardDeletePolicy.Forbidden))
            Assert.Contains(table, deleteGuarded);
    }

    private static async Task<List<string>> ColumnAsync(NpgsqlConnection connection, string sql)
    {
        var result = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }
}
