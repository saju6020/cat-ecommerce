using CatEcommerce.Domain.Aggregates.StoredFile;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>File Storage (HO-FIL-001) DbSets. See ADR-341 for the per-area partial pattern.</summary>
public sealed partial class CatEcommerceDbContext
{
    public DbSet<StoredFileAggregate> StoredFiles => Set<StoredFileAggregate>();
}
