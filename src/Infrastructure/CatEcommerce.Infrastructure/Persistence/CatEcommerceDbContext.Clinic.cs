using CatEcommerce.Domain.Aggregates.Clinic;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>Clinic Appointments (HO-APT-001) DbSets. See ADR-341 for the per-area partial pattern.</summary>
public sealed partial class CatEcommerceDbContext
{
    public DbSet<AppointmentAggregate> Appointments => Set<AppointmentAggregate>();
    public DbSet<WaitlistEntryAggregate> WaitlistEntries => Set<WaitlistEntryAggregate>();
}
