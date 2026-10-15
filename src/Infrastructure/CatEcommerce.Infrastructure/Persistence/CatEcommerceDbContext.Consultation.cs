using CatEcommerce.Domain.Aggregates.Visit;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>Consultation and Medical Records (HO-EMR-001) DbSets, added phase by phase. See ADR-341 for
/// the per-area partial pattern.</summary>
public sealed partial class CatEcommerceDbContext
{
    // Phase A -- catalogues
    public DbSet<DiagnosisCodeAggregate> DiagnosisCodes => Set<DiagnosisCodeAggregate>();
    public DbSet<LabAnalyteAggregate> LabAnalytes => Set<LabAnalyteAggregate>();
    public DbSet<NoteTemplateAggregate> NoteTemplates => Set<NoteTemplateAggregate>();

    // Phase B -- the visit and its owned collections
    public DbSet<VisitAggregate> Visits => Set<VisitAggregate>();

    // Phase C -- append-only addenda and the per-author working copy
    public DbSet<VisitAddendumAggregate> VisitAddenda => Set<VisitAddendumAggregate>();
    public DbSet<ConsultationDraftAggregate> ConsultationDrafts => Set<ConsultationDraftAggregate>();

    // Phase D -- diagnostics (results append-only)
    public DbSet<DiagnosticOrderAggregate> DiagnosticOrders => Set<DiagnosticOrderAggregate>();

    // Phase H -- consent (a Regulatory record, never deleted)
    public DbSet<ConsentRecordAggregate> ConsentRecords => Set<ConsentRecordAggregate>();
}
