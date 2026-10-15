using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Infrastructure.Operations;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>Cross-cutting / Operations (HO-XCT-001, ADR-297) DbSets. See ADR-341 for the per-area partial pattern.</summary>
public sealed partial class CatEcommerceDbContext
{
    public DbSet<ErasureRequestAggregate> ErasureRequests => Set<ErasureRequestAggregate>();
    public DbSet<RetentionRunAggregate> RetentionRuns => Set<RetentionRunAggregate>();
    public DbSet<WebVitalBatchAggregate> WebVitalBatches => Set<WebVitalBatchAggregate>();
    public DbSet<RestoreTestRecordAggregate> RestoreTestRecords => Set<RestoreTestRecordAggregate>();

    /// <summary>Read model over a table only the backup job writes (ADR-312). Query with AsNoTracking().</summary>
    public DbSet<BackupRunReadModel> BackupRuns => Set<BackupRunReadModel>();
}
