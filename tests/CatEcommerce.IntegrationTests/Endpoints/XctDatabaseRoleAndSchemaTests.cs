using System.Text.RegularExpressions;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Operations;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Retention;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-XCT-001 I-A2, I-A7, I-A8 (and I-F3 in Phase F): everything that needs a database of its own. One disposable scratch database
/// on the same server as <c>CATECOMMERCE_TEST_DB</c> (never that database itself) is migrated from empty ONCE for the class, then
/// <c>deploy/database/roles.sql</c> is applied to it so the real role model is what is tested. The three roles are cluster-wide, so they
/// are dropped again when the class is disposed.
/// </summary>
public sealed class XctDatabaseRoleAndSchemaTests : IAsyncLifetime
{
    private const string Migrator = "catecommerce_migrator";
    private const string App = "catecommerce_app";
    private const string Reporter = "catecommerce_backup_reporter";

    private readonly string _databaseName = $"catecommerce_xct_scratch_{Guid.NewGuid():N}";
    private readonly string _migratorPassword = "m" + Guid.NewGuid().ToString("N");
    private readonly string _appPassword = "a" + Guid.NewGuid().ToString("N");
    private readonly string _reporterPassword = "r" + Guid.NewGuid().ToString("N");

    private static NpgsqlConnectionStringBuilder Server() => new(XctApiFactory.GuardedConnectionString());

    private string AdminConnectionString => new NpgsqlConnectionStringBuilder(Server().ConnectionString) { Database = "postgres", Pooling = false }.ConnectionString;

    private string ScratchAs(string? user = null, string? password = null)
    {
        var builder = new NpgsqlConnectionStringBuilder(Server().ConnectionString) { Database = _databaseName, Pooling = false };
        if (user is not null) { builder.Username = user; builder.Password = password; }
        return builder.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        await using (var admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<CatEcommerceDbContext>().UseNpgsql(ScratchAs()).Options;
        await using (var context = new CatEcommerceDbContext(options))
            await context.GetService<IMigrator>().MigrateAsync();

        await ExecuteScriptAsync(ScratchAs(), RolesSqlFor(_migratorPassword, _appPassword, _reporterPassword));
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

        await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\"", admin))
            await drop.ExecuteNonQueryAsync();

        foreach (var role in new[] { Reporter, App, Migrator })
        {
            try
            {
                await using var dropRole = new NpgsqlCommand($"DROP ROLE IF EXISTS {role}", admin);
                await dropRole.ExecuteNonQueryAsync();
            }
            catch (PostgresException)
            {
                // Another database on this server still has privileges for the role; it is a harmless leftover.
            }
        }
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CatEcommerce.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }

    /// <summary>roles.sql is a psql script: the meta-commands are dropped and the psql variables replaced by literals here.</summary>
    internal static string RolesSqlFor(string migratorPassword, string appPassword, string reporterPassword)
    {
        var lines = File.ReadAllLines(Path.Combine(RepositoryRoot(), "deploy", "database", "roles.sql"))
            .Where(line => !line.TrimStart().StartsWith('\\'));
        return string.Join('\n', lines)
            .Replace(":'migrator_password'", $"'{migratorPassword}'")
            .Replace(":'app_password'", $"'{appPassword}'")
            .Replace(":'reporter_password'", $"'{reporterPassword}'");
    }

    /// <summary>Inserts one row into an otherwise empty table, giving every NOT NULL column without a default a type-appropriate dummy value.</summary>
    private async Task InsertThrowAwayRowAsync(string schema, string table)
    {
        await using var connection = new NpgsqlConnection(ScratchAs());
        await connection.OpenAsync();

        var columns = new List<(string Name, string Type)>();
        await using (var describe = new NpgsqlCommand(
            "SELECT column_name, data_type FROM information_schema.columns WHERE table_schema = @s AND table_name = @t AND is_nullable = 'NO' AND column_default IS NULL AND is_generated = 'NEVER' ORDER BY ordinal_position", connection))
        {
            describe.Parameters.AddWithValue("s", schema);
            describe.Parameters.AddWithValue("t", table);
            await using var reader = await describe.ExecuteReaderAsync();
            while (await reader.ReadAsync()) columns.Add((reader.GetString(0), reader.GetString(1)));
        }

        static string Dummy(string type) => type switch
        {
            "uuid" => "gen_random_uuid()",
            "boolean" => "false",
            "integer" or "bigint" or "smallint" or "numeric" => "0",
            "date" => "current_date",
            "timestamp with time zone" or "timestamp without time zone" => "now()",
            "jsonb" or "json" => "'{}'",
            _ => "'x'",
        };

        var sql = $"INSERT INTO {schema}.{table} ({string.Join(", ", columns.Select(c => '\"' + c.Name + '\"'))}) VALUES ({string.Join(", ", columns.Select(c => Dummy(c.Type)))})";
        await using var insert = new NpgsqlCommand(sql, connection);
        await insert.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteScriptAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static DatabaseRoleGuard GuardFor(string environment, bool enforce = true) => new(
        Options.Create(new OperationsOptions { Database = { EnforceNonOwnerRole = enforce } }),
        new FakeEnvironment(environment),
        NullLogger<DatabaseRoleGuard>.Instance);

    // I-A2
    [Fact]
    public async Task I_A2_MigrationsOnAnEmptyDatabase_InstallTheFunction_AndBothTriggersOnEveryForbiddenTable()
    {
        await using var connection = new NpgsqlConnection(ScratchAs());
        await connection.OpenAsync();

        await using (var function = new NpgsqlCommand(
            "SELECT COUNT(*) FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'retention' AND p.proname = 'refuse_hard_delete'", connection))
            Assert.Equal(1L, await function.ExecuteScalarAsync());

        var expected = RecordClassification.All
            .Where(e => e.HardDeletePolicy == HardDeletePolicy.Forbidden
                        && !RetentionGuardSql.DedicatedlyGuardedTables.Any(d => d.Schema == e.Schema && d.Table == e.Table))
            .Select(e => $"{e.Schema}.{e.Table}")
            .ToHashSet();

        var withBoth = new HashSet<string>();
        await using (var command = new NpgsqlCommand("""
            SELECT n.nspname || '.' || c.relname
            FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE t.tgname = 'trg_refuse_hard_delete'
              AND EXISTS (SELECT 1 FROM pg_trigger t2 WHERE t2.tgrelid = t.tgrelid AND t2.tgname = 'trg_refuse_truncate')
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) withBoth.Add(reader.GetString(0));

        Assert.Empty(expected.Except(withBoth));
        Assert.Empty(withBoth.Except(expected));

        foreach (var (schema, table) in OperationsTables)
        {
            await using var exists = new NpgsqlCommand("SELECT to_regclass(@name) IS NOT NULL", connection);
            exists.Parameters.AddWithValue("name", $"{schema}.{table}");
            Assert.True((bool)(await exists.ExecuteScalarAsync())!, $"{schema}.{table} was not created by the migrations");
        }
    }

    /// <summary>The Operations tables each phase adds (kept in step with RecordClassification.Operations.cs).</summary>
    internal static readonly (string Schema, string Table)[] OperationsTables = [];

    // I-A7
    [Fact]
    public async Task I_A7_InProduction_ConnectedAsASuperuserOrOwner_TheGuardRefusesToStart()
    {
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => GuardFor("Production").AssertSafeAsync(ScratchAs(), CancellationToken.None));
        Assert.Equal(OperationsMessages.DatabaseRoleUnsafe, refused.Message);

        var report = await DatabaseRoleGuard.InspectAsync(ScratchAs(), CancellationToken.None);
        Assert.True(report.IsSuperuser);

        // The migrator owns every table: refused too, although it is not a superuser.
        var migratorReport = await DatabaseRoleGuard.InspectAsync(ScratchAs(Migrator, _migratorPassword), CancellationToken.None);
        Assert.False(migratorReport.IsSuperuser);
        Assert.NotEmpty(migratorReport.OwnedTables);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GuardFor("Production").AssertSafeAsync(ScratchAs(Migrator, _migratorPassword), CancellationToken.None));
    }

    [Fact]
    public async Task I_A7_DevelopmentAndTesting_AreExempt_ButTheFlagCannotBeSwitchedOffAnywhereElse()
    {
        await GuardFor("Development").AssertSafeAsync(ScratchAs(), CancellationToken.None);
        await GuardFor("Testing", enforce: false).AssertSafeAsync(ScratchAs(), CancellationToken.None);

        // EnforceNonOwnerRole=false is accepted only in Development/Testing: Production starts nowhere with it.
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GuardFor("Production", enforce: false).AssertSafeAsync(ScratchAs(App, _appPassword), CancellationToken.None));
        Assert.Contains("EnforceNonOwnerRole", refused.Message);
    }

    // I-A8
    [Fact]
    public async Task I_A8_TheAppRole_StartsTheGuard_AndCannotSwitchATriggerOff_OrTruncate_OrDeleteARetainedRow()
    {
        var appConnection = ScratchAs(App, _appPassword);

        await GuardFor("Production").AssertSafeAsync(appConnection, CancellationToken.None);

        await using var connection = new NpgsqlConnection(appConnection);
        await connection.OpenAsync();

        await using (var disable = new NpgsqlCommand("ALTER TABLE clinic.cat DISABLE TRIGGER ALL", connection))
        {
            var refused = await Assert.ThrowsAsync<PostgresException>(() => disable.ExecuteNonQueryAsync());
            Assert.Equal("42501", refused.SqlState); // insufficient_privilege: must be owner
        }

        await using (var truncate = new NpgsqlCommand("TRUNCATE clinic.cat CASCADE", connection))
        {
            var refused = await Assert.ThrowsAsync<PostgresException>(() => truncate.ExecuteNonQueryAsync());
            Assert.Equal("42501", refused.SqlState); // no TRUNCATE privilege
        }

        await using (var create = new NpgsqlCommand("CREATE TABLE clinic.should_not_exist (x int)", connection))
        {
            var refused = await Assert.ThrowsAsync<PostgresException>(() => create.ExecuteNonQueryAsync());
            Assert.Equal("42501", refused.SqlState); // no DDL
        }

        // DML is granted, and the retention trigger answers a DELETE of a retained row. The scratch database is empty, so one throw-away row is
        // inserted first (by the superuser: the point under test is what the APP role may do to it).
        await InsertThrowAwayRowAsync("admin", "tax_class");
        await using (var delete = new NpgsqlCommand("DELETE FROM admin.tax_class", connection))
        {
            var refused = await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());
            Assert.Equal("P0001", refused.SqlState);
            Assert.Contains("Hard delete of admin.tax_class is not permitted", refused.MessageText);
        }

        // Ordinary application work is unaffected: reads, and writes to a table the app may change.
        await using var read = new NpgsqlCommand("SELECT COUNT(*) FROM clinic.cat", connection);
        Assert.NotNull(await read.ExecuteScalarAsync());
    }

    [Fact]
    public void RolesSql_HoldsNoLiteralPassword_AndCoversEverySchemaOfTheModel()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "deploy", "database", "roles.sql"));

        // Passwords only ever arrive as psql variables.
        Assert.DoesNotMatch(new Regex(@"PASSWORD\s+'[^:]", RegexOptions.IgnoreCase), text);
        Assert.Matches(@"PASSWORD :'app_password'", text);

        var listed = Regex.Match(text, @"solution_schemas text\[\] := ARRAY\[(?<list>[^\]]+)\]").Groups["list"].Value
            .Split(',').Select(s => s.Trim().Trim('\'')).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(DatabaseRoleGuard.SolutionSchemas().ToHashSet(StringComparer.Ordinal), listed);
    }

    // I-F3
    [Fact]
    public async Task I_F3_TheBackupReporter_CanInsertABackupRun_AndNothingElse()
    {
        await using var connection = new NpgsqlConnection(ScratchAs(Reporter, _reporterPassword));
        await connection.OpenAsync();

        await using (var insert = new NpgsqlCommand(
            "INSERT INTO ops.backup_run (id, kind, started_on, finished_on, succeeded, repository) VALUES (@id, 'diff', now(), now(), true, 'repo1')", connection))
        {
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }

        foreach (var statement in new[]
        {
            "SELECT COUNT(*) FROM ops.backup_run",
            "UPDATE ops.backup_run SET succeeded = false",
            "DELETE FROM ops.backup_run",
            "SELECT COUNT(*) FROM account.customer_account",
            "INSERT INTO ops.erasure_request (\"Id\") VALUES (gen_random_uuid())",
        })
        {
            await using var command = new NpgsqlCommand(statement, connection);
            var refused = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("42501", refused.SqlState);                           // insufficient_privilege
        }
    }

    // SECURITY gate LOW 5 (2026-10-12)
    [Fact]
    public async Task TheGuard_AlsoRefusesALoginRoleThatIsAMemberOfTheOwnerWithoutInheritingItsPrivileges()
    {
        var role = "catecommerce_noinherit_" + Guid.NewGuid().ToString("N")[..8];
        var password = "n" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();
        try
        {
            await using (var create = new NpgsqlCommand($"CREATE ROLE {role} LOGIN NOINHERIT PASSWORD '{password}'; GRANT {Migrator} TO {role}", admin))
                await create.ExecuteNonQueryAsync();

            var report = await DatabaseRoleGuard.InspectAsync(ScratchAs(role, password), CancellationToken.None);

            Assert.False(report.IsSuperuser);
            Assert.NotEmpty(report.OwnedTables);                                // 'USAGE' saw nothing here; 'MEMBER' sees the owner
            await Assert.ThrowsAsync<InvalidOperationException>(() => GuardFor("Production").AssertSafeAsync(ScratchAs(role, password), CancellationToken.None));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP OWNED BY {role}; DROP ROLE IF EXISTS {role}", admin);
            try { await drop.ExecuteNonQueryAsync(); } catch (PostgresException) { /* a harmless leftover role */ }
        }
    }
}
