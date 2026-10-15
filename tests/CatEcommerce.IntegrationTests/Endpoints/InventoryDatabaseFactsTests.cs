using Npgsql;
using Xunit.Abstractions;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-IMP-001 §7 FI-1 ... FI-5 -- the five database facts the whole stock design depends on,
/// verified against a scratch schema (created and dropped by each test, never touching a real
/// table) on whatever database <c>CATECOMMERCE_TEST_DB</c> names. Two sessions are needed for the
/// concurrency facts, so the data is committed and the scratch schema dropped afterwards (the
/// rolled-back-transaction approach of §7 cannot show a second session blocking).
/// </summary>
public sealed class InventoryDatabaseFactsTests(ITestOutputHelper output)
{
    // The same guard as the Inventory API factory: CATECOMMERCE_TEST_DB must be set and must not name catecommerce_dev (these tests create and drop scratch schemas).
    private static readonly string ConnectionString = InventoryApiFactory.RequireBranchDatabase();

    private static async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecAsync(NpgsqlConnection c, string sql, NpgsqlTransaction? tx = null)
    {
        await using var cmd = new NpgsqlCommand(sql, c, tx);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string NewSchema() => "fi_" + Guid.NewGuid().ToString("N")[..12];

    /// <summary>The literal trigger shape of §7 FI-1 (INSERT ... ON CONFLICT DO UPDATE), or the shape the build uses.</summary>
    private static string Setup(string s, bool literal)
    {
        var apply = literal
            ? $"INSERT INTO {s}.position (k, on_hand) VALUES (NEW.k, NEW.delta) ON CONFLICT (k) DO UPDATE SET on_hand = {s}.position.on_hand + EXCLUDED.on_hand;"
            : $"INSERT INTO {s}.position (k, on_hand) VALUES (NEW.k, 0) ON CONFLICT (k) DO NOTHING; UPDATE {s}.position SET on_hand = on_hand + NEW.delta WHERE k = NEW.k;";

        return $@"
CREATE SCHEMA {s};
CREATE TABLE {s}.position (k int PRIMARY KEY, on_hand numeric(12,3) NOT NULL DEFAULT 0, CONSTRAINT ck_position CHECK (on_hand >= 0));
CREATE TABLE {s}.ledger (id serial PRIMARY KEY, k int NOT NULL, delta numeric(12,3) NOT NULL);
CREATE FUNCTION {s}.apply() RETURNS trigger LANGUAGE plpgsql AS $f$
BEGIN
  {apply}
  RETURN NEW;
END $f$;
CREATE TRIGGER tg_apply AFTER INSERT ON {s}.ledger FOR EACH ROW EXECUTE FUNCTION {s}.apply();";
    }

    /// <summary>Two concurrent -1 entries against a position holding 1.</summary>
    private static async Task<(int Winners, List<string> Losers)> RaceAsync(string schema)
    {
        await using (var seed = await OpenAsync())
            await ExecAsync(seed, $"INSERT INTO {schema}.ledger (k, delta) VALUES (1, 1);");

        await using var c1 = await OpenAsync();
        await using var c2 = await OpenAsync();
        await using var t1 = await c1.BeginTransactionAsync();
        await using var t2 = await c2.BeginTransactionAsync();

        await ExecAsync(c1, $"INSERT INTO {schema}.ledger (k, delta) VALUES (1, -1);", t1);

        var second = Task.Run(async () =>
        {
            try { await ExecAsync(c2, $"INSERT INTO {schema}.ledger (k, delta) VALUES (1, -1);", t2); return "ok"; }
            catch (PostgresException ex) { return $"{ex.SqlState}:{ex.ConstraintName}"; }
        });

        await Task.Delay(750);
        Assert.False(second.IsCompleted, "FI-1: the second concurrent writer must BLOCK on the first's row lock.");

        await t1.CommitAsync();
        var outcome = await second;
        if (outcome == "ok") await t2.CommitAsync(); else await t2.RollbackAsync();

        return (outcome == "ok" ? 2 : 1, outcome == "ok" ? [] : [outcome]);
    }

    [Fact]
    public async Task FI1_LiteralOnConflictShape_Observed()
    {
        var schema = NewSchema();
        await using var admin = await OpenAsync();
        await ExecAsync(admin, Setup(schema, literal: true));
        try
        {
            var seedFailure = await Record.ExceptionAsync(async () =>
            {
                await using var seed = await OpenAsync();
                await ExecAsync(seed, $"INSERT INTO {schema}.ledger (k, delta) VALUES (1, 1);");
            });
            output.WriteLine("literal shape, seed +1: " + (seedFailure?.Message ?? "ok"));

            var single = await Record.ExceptionAsync(async () =>
            {
                await using var c = await OpenAsync();
                await ExecAsync(c, $"INSERT INTO {schema}.ledger (k, delta) VALUES (1, -1);");
            });
            output.WriteLine("literal shape, single-session -1 against on_hand 1: " + (single is PostgresException p ? $"{p.SqlState}:{p.ConstraintName}" : "ok"));

            // FINDING (recorded in HO-IMP-001 §1 Amended): the CHECK is evaluated against the PROPOSED
            // insert tuple before ON CONFLICT is considered, so the literal FI-1 shape refuses EVERY
            // deduction -- even one that fits -- with a spurious 23514. The build therefore uses the
            // equivalent two-statement shape (insert a zero row ON CONFLICT DO NOTHING, then UPDATE),
            // which FI1_LastUnitRace_SecondWriterBlocksThenFailsTheCheck proves has FI-1's consequence.
            var refused = Assert.IsType<PostgresException>(single);
            Assert.Equal("23514", refused.SqlState);
        }
        finally { await ExecAsync(admin, $"DROP SCHEMA {schema} CASCADE;"); }
    }

    [Fact]
    public async Task FI1_LastUnitRace_SecondWriterBlocksThenFailsTheCheck()
    {
        var schema = NewSchema();
        await using var admin = await OpenAsync();
        await ExecAsync(admin, Setup(schema, literal: false));
        try
        {
            var (winners, losers) = await RaceAsync(schema);
            Assert.Equal(1, winners);
            Assert.Equal("23514:ck_position", Assert.Single(losers));
        }
        finally { await ExecAsync(admin, $"DROP SCHEMA {schema} CASCADE;"); }
    }

    [Fact]
    public async Task FI2_RaiseExceptionWithConstraintName_SurfacesAsPostgresException()
    {
        await using var c = await OpenAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() => ExecAsync(c, @"
DO $$ BEGIN RAISE EXCEPTION 'expired' USING ERRCODE = '23514', CONSTRAINT = 'tg_stock_batch_expired'; END $$;"));
        Assert.Equal("23514", ex.SqlState);
        Assert.Equal("tg_stock_batch_expired", ex.ConstraintName);
    }

    [Fact]
    public async Task FI4_TimeZoneNamesResolveOnTheServer()
    {
        await using var c = await OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT (TIMESTAMPTZ '2026-10-11 18:30:00+00' AT TIME ZONE 'Asia/Dhaka')::date, (TIMESTAMPTZ '2026-07-01 23:30:00+00' AT TIME ZONE 'Europe/London')::date", c);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(new DateOnly(2026, 10, 12), reader.GetFieldValue<DateOnly>(0));
        Assert.Equal(new DateOnly(2026, 7, 2), reader.GetFieldValue<DateOnly>(1));
    }

    [Fact]
    public async Task FI5_ForShareInTheLedgerTrigger_MakesAConcurrentBatchUpdateWait()
    {
        var schema = NewSchema();
        await using var admin = await OpenAsync();
        await ExecAsync(admin, $@"
CREATE SCHEMA {schema};
CREATE TABLE {schema}.batch (id int PRIMARY KEY, status int NOT NULL);
CREATE TABLE {schema}.ledger (id serial PRIMARY KEY, batch_id int NOT NULL, delta numeric NOT NULL);
INSERT INTO {schema}.batch VALUES (1, 1);
CREATE FUNCTION {schema}.usable() RETURNS trigger LANGUAGE plpgsql AS $f$
DECLARE s int;
BEGIN
  SELECT status INTO s FROM {schema}.batch WHERE id = NEW.batch_id FOR SHARE;
  IF s <> 1 THEN RAISE EXCEPTION 'not usable' USING ERRCODE = '23514', CONSTRAINT = 'tg_stock_batch_not_usable'; END IF;
  RETURN NEW;
END $f$;
CREATE TRIGGER tg_usable BEFORE INSERT ON {schema}.ledger FOR EACH ROW EXECUTE FUNCTION {schema}.usable();");
        try
        {
            await using var consumer = await OpenAsync();
            await using var recaller = await OpenAsync();
            await using var consumerTx = await consumer.BeginTransactionAsync();
            await ExecAsync(consumer, $"INSERT INTO {schema}.ledger (batch_id, delta) VALUES (1, -1);", consumerTx);

            var recall = Task.Run(() => ExecAsync(recaller, $"UPDATE {schema}.batch SET status = 3 WHERE id = 1;"));
            await Task.Delay(750);
            Assert.False(recall.IsCompleted, "FI-5: the recall UPDATE must wait for the in-flight consumer.");
            await consumerTx.CommitAsync();
            await recall;

            var refused = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecAsync(consumer, $"INSERT INTO {schema}.ledger (batch_id, delta) VALUES (1, -1);"));
            Assert.Equal("23514", refused.SqlState);
            Assert.Equal("tg_stock_batch_not_usable", refused.ConstraintName);
        }
        finally { await ExecAsync(admin, $"DROP SCHEMA {schema} CASCADE;"); }
    }
}
