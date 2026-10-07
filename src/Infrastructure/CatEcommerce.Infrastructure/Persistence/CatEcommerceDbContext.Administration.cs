using CatEcommerce.Domain.Aggregates.Administration;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>Administration and Configuration (HO-ADM-001) DbSets. See ADR-341 for the per-area
/// partial pattern.</summary>
public sealed partial class CatEcommerceDbContext
{
    public DbSet<BranchAggregate> Branches => Set<BranchAggregate>();
    public DbSet<BranchClosureAggregate> BranchClosures => Set<BranchClosureAggregate>();
    public DbSet<RoomAggregate> Rooms => Set<RoomAggregate>();
    public DbSet<StaffBranchAssignmentAggregate> StaffBranchAssignments => Set<StaffBranchAssignmentAggregate>();
    public DbSet<StaffRosterAggregate> StaffRosters => Set<StaffRosterAggregate>();
    public DbSet<RosterExceptionAggregate> RosterExceptions => Set<RosterExceptionAggregate>();
    public DbSet<LeaveRequestAggregate> LeaveRequests => Set<LeaveRequestAggregate>();
    public DbSet<ClinicServiceAggregate> ClinicServices => Set<ClinicServiceAggregate>();
    public DbSet<ServicePriceAggregate> ServicePrices => Set<ServicePriceAggregate>();
    public DbSet<TaxClassAggregate> TaxClasses => Set<TaxClassAggregate>();
    public DbSet<TaxRateAggregate> TaxRates => Set<TaxRateAggregate>();
    public DbSet<BusinessSettingsAggregate> BusinessSettings => Set<BusinessSettingsAggregate>();
    public DbSet<ContentItemAggregate> ContentItems => Set<ContentItemAggregate>();
    public DbSet<PolicyDocumentAggregate> PolicyDocuments => Set<PolicyDocumentAggregate>();
    public DbSet<PolicyVersionAggregate> PolicyVersions => Set<PolicyVersionAggregate>();
    public DbSet<PolicyAcceptanceAggregate> PolicyAcceptances => Set<PolicyAcceptanceAggregate>();
    public DbSet<SystemStatusAggregate> SystemStatuses => Set<SystemStatusAggregate>();
}
