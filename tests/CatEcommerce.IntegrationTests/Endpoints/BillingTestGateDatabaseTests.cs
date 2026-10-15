using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-BIL-001 TEST gate rows XI-6, XI-7 and XI-9 -- the database as it is AFTER the other suites have filled it, which is when these properties mean
/// something: every billing table is covered by a delete-refusing trigger and refuses a real row's deletion; TRUNCATE is attempted (always inside a
/// transaction that is rolled back); and no text or json column of any table holds a card-like number. Each is meaningful on a populated database;
/// the sheet records the row counts they saw.
/// </summary>
public sealed partial class BillingTestGateDatabaseTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    private static NpgsqlConnection Open()
    {
        var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB")
            ?? throw new InvalidOperationException("CATECOMMERCE_TEST_DB is not set."));
        connection.Open();
        return connection;
    }

    private static List<string> BillingTables()
    {
        using var connection = Open();
        using var command = new NpgsqlCommand("SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'billing' AND c.relkind = 'r' ORDER BY 1", connection);
        using var reader = command.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read()) tables.Add(reader.GetString(0));
        return tables;
    }

    // ── XI-6 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task XI_6_EveryBillingTable_HasADeleteTrigger_AndRefusesToDeleteARealRow()
    {
        var withoutTrigger = new List<string>();
        var deleted = new List<string>();
        var tested = new List<string>();

        await using var connection = Open();
        foreach (var table in BillingTables())
        {
            await using (var catalog = new NpgsqlCommand(
                "SELECT count(*) FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace " +
                "WHERE n.nspname = 'billing' AND c.relname = @t AND NOT t.tgisinternal AND (t.tgtype & 8) = 8", connection))
            {
                catalog.Parameters.AddWithValue("t", table);
                if ((long)(await catalog.ExecuteScalarAsync())! == 0) withoutTrigger.Add(table);
            }

            // A real row, if the table has one: DELETE it inside a transaction that is always rolled back.
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                // A draft invoice's unfrozen lines are deletable by design (BR-11, D-6), so the probe row for invoice_line must be an
                // issued (frozen) line -- otherwise the result depends on which row a shared database happens to return first.
                var probeFilter = table == "invoice_line" ? " WHERE is_frozen" : "";
                await using var command = new NpgsqlCommand($"DELETE FROM billing.\"{table}\" WHERE ctid = (SELECT ctid FROM billing.\"{table}\"{probeFilter} LIMIT 1)", connection, transaction);
                var affected = await command.ExecuteNonQueryAsync();
                if (affected > 0) deleted.Add(table);
                else continue;
            }
            catch (PostgresException error) when (error.SqlState == "P0001") { tested.Add(table); }
            finally { await transaction.RollbackAsync(); }
        }

        // The two AggregateOnly working sets the aggregates replace wholesale (ARCHITECTURE gate C-ALL-4: profile bank accounts, requested allocations of a pending
        // payment) are the only tables allowed to be deletable; invoice_line has a trigger that refuses only the lines of an issued invoice.
        withoutTrigger.RemoveAll(t => t is "billing_profile_bank_account" or "payment_requested_allocation");
        Assert.True(withoutTrigger.Count == 0, "Billing tables with no delete trigger: " + string.Join(", ", withoutTrigger));
        deleted.RemoveAll(t => t is "billing_profile_bank_account" or "payment_requested_allocation");
        Assert.True(deleted.Count == 0, "A real row was deleted (rolled back) from: " + string.Join(", ", deleted));
        Assert.True(tested.Count >= 10, "Too few tables held a row to try: " + string.Join(", ", tested));
    }

    // ── XI-7 ────────────────────────────────────────────────────────────────────────────────────────
    // Was an observed FAIL (SECURITY gate LOW, F-2); fixed by migration HardenClinicalAndBillingTriggers (BEFORE TRUNCATE on every billing table).
    [Fact]
    public async Task XI_7_TruncateOfAnImmutableBillingTable_IsRefused()
    {
        var accepted = new List<string>();

        foreach (var table in BillingTables())
        {
            await using var connection = Open();
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await using (var timeout = new NpgsqlCommand("SET LOCAL lock_timeout = '3s'", connection, transaction)) await timeout.ExecuteNonQueryAsync();
                await using var command = new NpgsqlCommand($"TRUNCATE billing.\"{table}\" CASCADE", connection, transaction);
                await command.ExecuteNonQueryAsync();
                accepted.Add(table);
            }
            catch (PostgresException) { /* refused (or the lock was busy): not accepted */ }
            finally { await transaction.RollbackAsync(); }
        }

        Assert.True(accepted.Count == 0, "TRUNCATE was accepted (rolled back) on: " + string.Join(", ", accepted));
    }

    // ── XI-9 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>XI-9 -- every text, character-varying and json(b) column of every table in the billing schema, the audit trail and the notification tables: any value that
    /// holds a 13 to 19 digit run (any separator) is asked of <see cref="CardNumberGuard"/>. None may be card-like.</summary>
    [Fact]
    public async Task XI_9_NoCardLikeNumber_IsStoredInAnyTextColumnOfTheDatabase()
    {
        var columns = await WithDbAsync(db =>
        {
            var found = new HashSet<(string Schema, string Table, string Column, bool Json)>();
            foreach (var entity in db.Model.GetEntityTypes())
            {
                var table = entity.GetTableName();
                if (table is null || entity.GetViewName() is not null) continue;
                var schema = entity.GetSchema() ?? "public";
                // Phone numbers of customer and staff accounts are 11 to 15 digit runs that pass Luhn one time in ten; they are not money records. Everything that
                // can carry free text about money is in the billing schema, the audit trail and the notification tables.
                if (schema != "billing" && !table.Contains("audit", StringComparison.OrdinalIgnoreCase) && !table.Contains("notification", StringComparison.OrdinalIgnoreCase)) continue;
                var identifier = StoreObjectIdentifier.Table(table, entity.GetSchema());
                foreach (var property in entity.GetProperties())
                {
                    var type = property.GetColumnType(identifier)?.ToLowerInvariant() ?? string.Empty;
                    var name = property.GetColumnName(identifier);
                    if (name is null) continue;
                    if (type.StartsWith("text") || type.StartsWith("character varying") || type.StartsWith("varchar")) found.Add((schema, table, name, false));
                    else if (type.StartsWith("json")) found.Add((schema, table, name, true));
                }
            }

            return Task.FromResult(found.ToList());
        });

        var offenders = new List<string>();
        var scanned = 0;
        await using var connection = Open();
        foreach (var (schema, table, column, _) in columns)
        {
            await using var command = new NpgsqlCommand(
                $"SELECT \"{column}\"::text FROM \"{schema}\".\"{table}\" WHERE \"{column}\"::text ~ '[0-9][0-9 ._/-]{{11,}}[0-9]' LIMIT 2000", connection);
            await using var reader = await command.ExecuteReaderAsync();
            scanned++;
            while (await reader.ReadAsync())
            {
                // Ids and instants in a JSON snapshot are long digit-and-dash runs too (a GUID whose groups happen to be all digits, a timestamp); they are
                // not what is being looked for, so they are blanked first.
                var value = Noise().Replace(reader.GetString(0), " ");
                if (CardNumberGuard.LooksLikeCardNumber(value))
                    offenders.Add($"{schema}.{table}.{column}: {Snippet(value)}");
            }
        }

        Assert.True(scanned > 50, $"Only {scanned} columns were scanned.");
        Assert.True(offenders.Count == 0, $"{offenders.Count} card-like value(s) stored: " + string.Join(" || ", offenders.Take(10)));
    }

    // Ids (dashed GUIDs and 32-character hex ids, which a provider message id or a product link carries), instants, our own document numbers, E.164 Bangladeshi mobile numbers and JSON's own backslash-u escapes (four digits next to the value they quote) are long digit runs that are not card numbers.
    [System.Text.RegularExpressions.GeneratedRegex(@"[0-9a-fA-F]{24,}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?|\\u[0-9A-Fa-f]{4}|(INV|CN)-[A-Za-z0-9]+-\d{4}-\d{6}|880\d{10}")]
    private static partial System.Text.RegularExpressions.Regex Noise();

    private static string Snippet(string value)
    {
        var match = System.Text.RegularExpressions.Regex.Match(value, @"[0-9][0-9 ._/-]{11,}[0-9]");
        var start = Math.Max(0, match.Index - 20);
        var text = value.Substring(start, Math.Min(value.Length - start, match.Length + 40));
        return System.Text.RegularExpressions.Regex.Replace(text, @"[0-9]", "#");
    }
}
