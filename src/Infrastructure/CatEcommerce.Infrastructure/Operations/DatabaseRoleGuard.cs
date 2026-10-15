using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CatEcommerce.Infrastructure.Operations;

/// <summary>What the guard found about the connected role.</summary>
public sealed record DatabaseRoleReport(string RoleName, bool IsSuperuser, IReadOnlyList<string> OwnedTables)
{
    public bool IsSafe => !IsSuperuser && OwnedTables.Count == 0;
}

/// <summary>
/// HO-XCT-001 §12.2 / ADR-300. A table owner can switch a trigger off with an ALTER TABLE statement (F35), and a superuser can set
/// the session replication role so that row triggers are skipped, so the retention triggers protect only when the runtime role is neither. Outside
/// Development and Testing the host refuses to start unless the current role is a non-owner, non-superuser role
/// (<c>catecommerce_app</c>, <c>deploy/database/roles.sql</c>). Ownership counts through role membership with inherited
/// privileges (<c>pg_has_role ... MEMBER</c>: gate fix 2026-10-12), so a login role that is a member of the owning role, inheriting or not (NOINHERIT can still SET ROLE), is also refused.
/// Never applies a migration and never changes anything: it only reads <c>pg_roles</c> and <c>pg_tables</c>.
/// </summary>
public sealed class DatabaseRoleGuard(
    IOptions<OperationsOptions> options,
    IHostEnvironment environment,
    ILogger<DatabaseRoleGuard> logger)
{
    private const string Sql = """
        SELECT r.rolname, r.rolsuper,
               COALESCE((SELECT array_agg(t.schemaname || '.' || t.tablename ORDER BY t.schemaname, t.tablename)
                         FROM pg_tables t
                         WHERE t.schemaname = ANY(@schemas) AND pg_has_role(current_user, t.tableowner, 'MEMBER')), ARRAY[]::text[])
        FROM pg_roles r
        WHERE r.rolname = current_user
        """;

    /// <summary>The schemas the solution owns: every classified table's schema, plus the trigger function's.</summary>
    public static IReadOnlyList<string> SolutionSchemas() =>
        RecordClassification.All.Select(entry => entry.Schema).Append("retention").Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();

    /// <summary>Reads the connected role's superuser flag and the solution tables it owns (or inherits ownership of).</summary>
    public static async Task<DatabaseRoleReport> InspectAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(Sql, connection);
        command.Parameters.AddWithValue("schemas", SolutionSchemas().ToArray());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("The connected role was not found in pg_roles.");

        return new DatabaseRoleReport(reader.GetString(0), reader.GetBoolean(1), reader.GetFieldValue<string[]>(2));
    }

    /// <summary>True when the environment is exempt from the role check (Development and Testing only).</summary>
    public bool IsExemptEnvironment => environment.IsDevelopment() || string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Throws when the host must not start: the flag is off outside Development/Testing, or the role is unsafe.
    /// Logs the role name and a count only -- never the connection string.
    /// </summary>
    public async Task AssertSafeAsync(string connectionString, CancellationToken ct)
    {
        var enforce = options.Value.Database.EnforceNonOwnerRole;

        if (IsExemptEnvironment)
        {
            logger.LogInformation("Database role guard skipped in {Environment} (Operations:Database:EnforceNonOwnerRole={Enforce}).", environment.EnvironmentName, enforce);
            return;
        }

        if (!enforce)
            throw new InvalidOperationException("Operations:Database:EnforceNonOwnerRole may be false only in Development and Testing.");

        var report = await InspectAsync(connectionString, ct);
        if (report.IsSafe)
        {
            logger.LogInformation("Database role guard passed: role {Role} is neither superuser nor owner of a solution table.", report.RoleName);
            return;
        }

        logger.LogCritical("Database role guard failed: role {Role} superuser={IsSuperuser} ownedTables={OwnedCount}.", report.RoleName, report.IsSuperuser, report.OwnedTables.Count);
        throw new InvalidOperationException(OperationsMessages.DatabaseRoleUnsafe);
    }
}

/// <summary>Runs <see cref="DatabaseRoleGuard"/> once at host start (after the host is built, never applying migrations).</summary>
public sealed class DatabaseRoleGuardHostedService(
    DatabaseRoleGuard guard, Microsoft.Extensions.Configuration.IConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        guard.AssertSafeAsync(configuration["ConnectionStrings:CatEcommerce"] ?? string.Empty, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
