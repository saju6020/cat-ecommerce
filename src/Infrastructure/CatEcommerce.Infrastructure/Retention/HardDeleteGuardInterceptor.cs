using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CatEcommerce.Infrastructure.Retention;

/// <summary>
/// HO-XCT-001 §12.1 / ADR-299 layer 2. Refuses a tracked delete of a protected row <b>before SQL is sent</b>, so the caller
/// gets a clear error instead of a database trigger exception. Registered <b>first</b> in <c>AddInterceptors</c>, ahead of
/// the audit change-capture interceptor, so a refused delete is never audited as though it happened.
///
/// <list type="bullet">
/// <item><c>Forbidden</c> -- always refused.</item>
/// <item><c>AggregateOnly</c> -- allowed only for an owned entity whose owner is itself tracked and not deleted (an aggregate
/// method replacing its own current-state collection, F32).</item>
/// <item><c>Purgeable</c> -- refused through change tracking: purges are <c>ExecuteDelete</c> in <c>RetentionPurger</c>.
/// The one named exception is <see cref="ChangeTrackedPurgeAllowList"/> (a file row, deleted object-first by File Storage).</item>
/// </list>
///
/// <c>ExecuteDelete</c> and raw SQL bypass interceptors (F34); the database triggers and the source scan cover those.
/// </summary>
public sealed class HardDeleteGuardInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Purgeable tables whose rows the owning area deletes through change tracking, with a stated reason. Deviation from
    /// §12.1's "purgeable root via change tracking -> throw", recorded in ADR-299's amendment: HO-FIL-001 §8.7 deletes the
    /// stored object first and the <c>files.stored_file</c> row second through <c>StoredFileEraser</c> (the shipped
    /// <c>IStoredFileEraser</c>, shared by the delete command and <c>StoredFilePurgeSource</c>), and README C-5 /
    /// HO-XCT-001 §1 Amended make File Storage the purge source for that table.
    /// </summary>
    public static readonly IReadOnlyList<(string Schema, string Table, string Reason)> ChangeTrackedPurgeAllowList =
    [
        ("files", "stored_file", "HO-FIL-001 section 8.7 / ADR-318: object first, row second, via IStoredFileEraser; legal hold is enforced there."),
    ];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        EnsureNoProtectedDeletes(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        EnsureNoProtectedDeletes(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>Throws <see cref="ProtectedRecordDeletionException"/> for the first tracked delete the classification forbids.</summary>
    public static void EnsureNoProtectedDeletes(DbContext? context)
    {
        if (context is null) return;

        var deleted = context.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Deleted).ToList();
        if (deleted.Count == 0) return;

        foreach (var entry in deleted)
        {
            var entityType = entry.Metadata;
            var table = entityType.GetTableName();
            if (table is null) continue; // keyless view entity: no row to delete

            var schema = entityType.GetSchema() ?? "public";

            // An owned type sharing its owner's table (table splitting) is a column group, not a row: its "delete" is an UPDATE.
            if (IsTableSplitOwned(entityType)) continue;

            var classification = RecordClassification.Find(schema, table)
                ?? throw new InvalidOperationException(
                    $"Table {schema}.{table} has no RecordClassification entry; classify it in RecordClassification.[Area].cs (ADR-298).");

            var policy = classification.HardDeletePolicy;

            if (policy == HardDeletePolicy.Forbidden)
                throw new ProtectedRecordDeletionException(schema, table, policy);

            if (policy == HardDeletePolicy.AggregateOnly)
            {
                if (!OwnerIsTrackedAndNotDeleted(context, entry))
                    throw new ProtectedRecordDeletionException(schema, table, policy);
                continue;
            }

            // Purgeable
            if (!ChangeTrackedPurgeAllowList.Any(a => a.Schema == schema && a.Table == table))
                throw new ProtectedRecordDeletionException(schema, table, policy);
        }
    }

    private static bool IsTableSplitOwned(IEntityType entityType)
    {
        var ownership = entityType.FindOwnership();
        if (ownership is null) return false;
        var owner = ownership.PrincipalEntityType;
        return owner.GetTableName() == entityType.GetTableName() && owner.GetSchema() == entityType.GetSchema();
    }

    private static bool OwnerIsTrackedAndNotDeleted(DbContext context, EntityEntry entry)
    {
        var ownership = entry.Metadata.FindOwnership();
        if (ownership is null) return false;

        var foreignKeyValues = ownership.Properties.Select(property => entry.Property(property.Name).CurrentValue).ToArray();
        var keyProperties = ownership.PrincipalKey.Properties;

        foreach (var candidate in context.ChangeTracker.Entries())
        {
            if (candidate.Metadata != ownership.PrincipalEntityType || candidate.State == EntityState.Deleted) continue;

            var matches = true;
            for (var i = 0; i < keyProperties.Count; i++)
            {
                if (!Equals(candidate.Property(keyProperties[i].Name).CurrentValue, foreignKeyValues[i]))
                {
                    matches = false;
                    break;
                }
            }

            if (matches) return true;
        }

        return false;
    }
}
