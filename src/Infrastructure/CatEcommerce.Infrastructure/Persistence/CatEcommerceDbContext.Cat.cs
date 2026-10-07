using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Vaccination;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>Cat Profiles (HO-CAT-001) DbSets. See ADR-341 for the per-area partial pattern.</summary>
public sealed partial class CatEcommerceDbContext
{
    public DbSet<CatAggregate> Cats => Set<CatAggregate>();
    public DbSet<CatWeightRecordAggregate> CatWeightRecords => Set<CatWeightRecordAggregate>();
    public DbSet<VaccineAggregate> Vaccines => Set<VaccineAggregate>();
    public DbSet<AuditEntryAggregate> AuditEntries => Set<AuditEntryAggregate>();
}
