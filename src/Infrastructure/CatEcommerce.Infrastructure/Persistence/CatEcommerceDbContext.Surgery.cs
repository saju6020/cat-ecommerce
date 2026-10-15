using CatEcommerce.Domain.Aggregates.Surgery;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>Surgery (HO-SRG-001) DbSets -- one partial per area (ADR-341), added phase by phase. The model is discovered from the configurations; these
/// properties are the typed entry points.</summary>
public sealed partial class CatEcommerceDbContext
{
    // Phase B -- the surgery (with its checklist, follow-ups and histories)
    public DbSet<SurgeryAggregate> Surgeries => Set<SurgeryAggregate>();

    // Phase D -- the anaesthesia log (append-only; see the trigger in the AddAnaesthesiaLog migration)
    public DbSet<AnaesthesiaLogEntryAggregate> AnaesthesiaLogEntries => Set<AnaesthesiaLogEntryAggregate>();

    // Phase E -- the surgical report and its addenda (immutable once finalised; see the triggers in the AddSurgicalReport migration)
    public DbSet<SurgicalReportAggregate> SurgicalReports => Set<SurgicalReportAggregate>();

    // Phase A -- protocols
    public DbSet<SurgeryProtocolAggregate> SurgeryProtocols => Set<SurgeryProtocolAggregate>();

    // Phase G -- campaign price lists
    public DbSet<SurgeryCampaignAggregate> SurgeryCampaigns => Set<SurgeryCampaignAggregate>();
}
