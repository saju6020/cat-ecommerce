using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CatEcommerce.Infrastructure.Reporting;

/// <summary>Raised when a report statement exceeds the statement timeout (SQLSTATE 57014). Mapped to <c>400 ReportTimedOut</c> by the API filter (section 12.5).</summary>
public sealed class ReportTimedOutException(Exception inner) : Exception("The report statement exceeded the statement timeout.", inner);

/// <summary>
/// HO-RPT-001 section 8.5 / ADR-279 -- the only way a report source or dashboard tile touches the database. A <c>READ ONLY</c> transaction with a <c>SET LOCAL statement_timeout</c>, always
/// rolled back: an accidental write fails in the database (SQLSTATE 25006), not in code review.
/// </summary>
public interface IReportingReadScope
{
    Task<T> RunAsync<T>(Func<CatEcommerceDbContext, CancellationToken, Task<T>> work, CancellationToken ct);
}

public sealed class ReportingReadScope(CatEcommerceDbContext db, IOptions<ReportingOptions> options, ILogger<ReportingReadScope> logger) : IReportingReadScope
{
    private const string TimeoutSqlState = "57014";
    private const string ReadOnlySqlState = "25006";

    public async Task<T> RunAsync<T>(Func<CatEcommerceDbContext, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Plain ADO.NET on the context's own connection and transaction. The seconds value is an integer from validated options, never user input.
            var seconds = Math.Clamp(options.Value.QueryTimeoutSeconds, 5, 120);
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = $"SET TRANSACTION READ ONLY; SET LOCAL statement_timeout = '{seconds}s';";
                await command.ExecuteNonQueryAsync(ct);
            }

            return await work(db, ct);
        }
        catch (Exception ex) when (FindPostgres(ex) is { } postgres)
        {
            if (postgres.SqlState == TimeoutSqlState) throw new ReportTimedOutException(ex);

            if (postgres.SqlState == ReadOnlySqlState)
                logger.LogError("A report tried to write inside the read-only reporting scope (SQLSTATE 25006). This is a programming error.");

            throw;
        }
        finally
        {
            // Never commits: there is nothing to commit.
            await transaction.RollbackAsync(CancellationToken.None);
        }
    }

    private static PostgresException? FindPostgres(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
            if (current is PostgresException postgres) return postgres;

        return null;
    }
}
