using CatEcommerce.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>
/// One DbContext for the solution -- HO-CAT-001 section 12.1.
///
/// <c>partial</c> so each functional area (ADR-341) adds its own <c>DbSet</c> properties in its
/// own <c>CatEcommerceDbContext.[Area].cs</c> file (e.g. <c>CatEcommerceDbContext.Shop.cs</c>)
/// instead of editing this file. <see cref="OnModelCreating"/> discovers every
/// <see cref="Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{TEntity}"/> in this assembly,
/// so a new area needs no change here at all -- only its own DbSet partial file plus its
/// configuration classes. See ADR-341 and <c>.ai/decisions/adr.md</c>.
///
/// Only the integrator creates EF migrations (ADR-341) -- an area adds DbSets and configurations,
/// never a migration.
///
/// HO-SHP-001 section 13 names this file (not the area's own partial) for exactly the three
/// migration-created, read-only, keyless views it adds -- <see cref="StorefrontCatalogueEntry"/>,
/// <see cref="ProductRatingSummaryRow"/> and <see cref="CatalogueTermRow"/> -- since a view entity
/// set is a different kind of thing from an owned aggregate's DbSet.
/// </summary>
public sealed partial class CatEcommerceDbContext(DbContextOptions<CatEcommerceDbContext> options)
    : DbContext(options)
{
    public DbSet<StorefrontCatalogueEntry> StorefrontCatalogueEntries => Set<StorefrontCatalogueEntry>();
    public DbSet<ProductRatingSummaryRow> ProductRatingSummaries => Set<ProductRatingSummaryRow>();
    public DbSet<CatalogueTermRow> CatalogueTerms => Set<CatalogueTermRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatEcommerceDbContext).Assembly);
}
