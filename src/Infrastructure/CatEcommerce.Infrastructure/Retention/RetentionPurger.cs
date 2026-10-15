using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Infrastructure.Operations;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Retention;

/// <summary>
/// HO-XCT-001 section 8.9 / 12.2 / ADR-304 -- the ONLY class in <c>src/</c> that deletes rows with <c>ExecuteDelete</c> (the source scan U-A2 enforces
/// that). A named method per table, never a table name from outside, and every table it touches is asserted <c>Purgeable</c> in
/// <see cref="RecordClassification"/> before the first row goes: an <see cref="InvalidOperationException"/> otherwise. Each method selects at most
/// <c>batchSize</c> ids first and deletes exactly those, so a run is bounded and an error in one batch cannot cascade. A <c>Forbidden</c> table has no
/// method here, and the database triggers would refuse it if one were ever added.
/// </summary>
public sealed class RetentionPurger(CatEcommerceDbContext db) : IRetentionPurger
{
    /// <summary>Every (schema, table) this class deletes from. <c>RetentionGuardMigrationTests</c> (U-A8) asserts each is Purgeable.</summary>
    public static readonly IReadOnlyList<(string Schema, string Table)> PurgedTables =
    [
        ("account", "customer_session"),
        ("account", "staff_session"),
        ("account", "password_reset_token"),
        ("ops", "web_vital_batch"),
        ("ops", "backup_run"),
    ];

    private static void RequirePurgeable(string schema, string table)
    {
        var classification = RecordClassification.Find(schema, table);
        if (classification is null || classification.HardDeletePolicy != HardDeletePolicy.Purgeable)
            throw new InvalidOperationException($"{schema}.{table} is not classified Purgeable; the retention purger must not delete from it.");
    }

    public async Task<int> PurgeCustomerSessionsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
    {
        RequirePurgeable("account", "customer_session");

        var ids = await db.Set<CustomerSessionAggregate>().AsNoTracking()
            .Where(s => (s.EndedOn != null && s.EndedOn < cutoff) || s.ExpiresOn < cutoff)
            .OrderBy(s => s.ExpiresOn).Take(batchSize).Select(s => s.Id).ToListAsync(ct);

        return ids.Count == 0 ? 0 : await db.Set<CustomerSessionAggregate>().Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync(ct);
    }

    public async Task<int> PurgeStaffSessionsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
    {
        RequirePurgeable("account", "staff_session");

        var ids = await db.Set<StaffSessionAggregate>().AsNoTracking()
            .Where(s => (s.EndedOn != null && s.EndedOn < cutoff) || s.ExpiresOn < cutoff)
            .OrderBy(s => s.ExpiresOn).Take(batchSize).Select(s => s.Id).ToListAsync(ct);

        return ids.Count == 0 ? 0 : await db.Set<StaffSessionAggregate>().Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync(ct);
    }

    public async Task<int> PurgePasswordResetTokensAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
    {
        RequirePurgeable("account", "password_reset_token");

        var ids = await db.Set<PasswordResetTokenAggregate>().AsNoTracking()
            .Where(t => t.ExpiresOn < cutoff)
            .OrderBy(t => t.ExpiresOn).Take(batchSize).Select(t => t.Id).ToListAsync(ct);

        return ids.Count == 0 ? 0 : await db.Set<PasswordResetTokenAggregate>().Where(t => ids.Contains(t.Id)).ExecuteDeleteAsync(ct);
    }

    public async Task<int> PurgeWebVitalBatchesAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
    {
        RequirePurgeable("ops", "web_vital_batch");
        RequirePurgeable("ops", "web_vital_sample");

        var ids = await db.Set<WebVitalBatchAggregate>().AsNoTracking()
            .Where(b => b.ReceivedOn < cutoff)
            .OrderBy(b => b.ReceivedOn).Take(batchSize).Select(b => b.Id).ToListAsync(ct);

        return ids.Count == 0 ? 0 : await db.Set<WebVitalBatchAggregate>().Where(b => ids.Contains(b.Id)).ExecuteDeleteAsync(ct);
    }

    public async Task<int> PurgeBackupRunsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
    {
        RequirePurgeable("ops", "backup_run");

        var ids = await db.Set<BackupRunReadModel>().AsNoTracking()
            .Where(r => r.FinishedOn < cutoff)
            .OrderBy(r => r.FinishedOn).Take(batchSize).Select(r => r.Id).ToListAsync(ct);

        return ids.Count == 0 ? 0 : await db.Set<BackupRunReadModel>().Where(r => ids.Contains(r.Id)).ExecuteDeleteAsync(ct);
    }
}
