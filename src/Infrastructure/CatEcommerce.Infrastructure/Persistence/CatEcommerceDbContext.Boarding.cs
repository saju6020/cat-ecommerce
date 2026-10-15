using CatEcommerce.Domain.Aggregates.Boarding;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>Boarding (HO-BRD-001) DbSets, schema <c>boarding</c>. See ADR-341 for the per-area partial pattern. One set per root; the stay's bounded
/// children are owned types and have no set.</summary>
public sealed partial class CatEcommerceDbContext
{
    public DbSet<BoardingUnitAggregate> BoardingUnits => Set<BoardingUnitAggregate>();
    public DbSet<UnitMaintenanceBlockAggregate> BoardingMaintenanceBlocks => Set<UnitMaintenanceBlockAggregate>();
    public DbSet<BoardingRateCardAggregate> BoardingRateCards => Set<BoardingRateCardAggregate>();
    public DbSet<BoardingCareRoutineAggregate> BoardingCareRoutines => Set<BoardingCareRoutineAggregate>();
    public DbSet<BoardingStayAggregate> BoardingStays => Set<BoardingStayAggregate>();
    public DbSet<CareTaskAggregate> BoardingCareTasks => Set<CareTaskAggregate>();
    public DbSet<CareObservationAggregate> BoardingCareObservations => Set<CareObservationAggregate>();
    public DbSet<IsolationEntryAggregate> BoardingIsolationEntries => Set<IsolationEntryAggregate>();
    public DbSet<BoardingIncidentAggregate> BoardingIncidents => Set<BoardingIncidentAggregate>();
    public DbSet<StayMediaAggregate> BoardingStayMedia => Set<StayMediaAggregate>();
    public DbSet<BoardingWaitlistEntryAggregate> BoardingWaitlistEntries => Set<BoardingWaitlistEntryAggregate>();
    public DbSet<DaycareRateCardAggregate> DaycareRateCards => Set<DaycareRateCardAggregate>();
    public DbSet<DaycareVisitAggregate> DaycareVisits => Set<DaycareVisitAggregate>();
}
