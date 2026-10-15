using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Read.ViewModels;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Surgery;

/// <summary>HO-SRG-001 §12.6 (US-SRG-05) -- the report with its addenda, the cat and the visit it attaches to, and the names of the people. Branch-scoped through the surgery.</summary>
public sealed class GetSurgicalReportQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetSurgicalReportQuery, SurgicalReportViewModel?>
{
    public async Task<QueryResponse<SurgicalReportViewModel?>> HandleAsync(GetSurgicalReportQuery query)
    {
        var inBranch = await db.Surgeries.AsNoTracking().AnyAsync(s => s.Id == query.SurgeryId && s.BranchId == query.BranchId && !s.IsMarkedToDelete);
        if (!inBranch) return new QueryResponse<SurgicalReportViewModel?> { Result = null };

        var report = await db.Set<SurgicalReportAggregate>().AsNoTracking().Where(r => r.Id == query.SurgeryId && !r.IsMarkedToDelete)
            .Select(r => new
            {
                r.Id, r.CatId, r.VisitId, r.BranchId, r.Version, r.ProcedurePerformed, r.Findings, r.Technique, r.SutureMaterial, r.HadComplications, r.Complications, r.SurgeonId, r.AssistantId,
                r.FinalisedOn, r.FinalisedBy, Addenda = r.Addenda.OrderBy(a => a.AddedOn).Select(a => new { a.Id, a.Text, a.AddedBy, a.AddedOn }).ToList()
            })
            .FirstOrDefaultAsync();
        if (report is null) return new QueryResponse<SurgicalReportViewModel?> { Result = null };

        var ids = new List<Guid> { report.SurgeonId, report.AssistantId };
        if (report.FinalisedBy is { } finalisedBy) ids.Add(finalisedBy);
        ids.AddRange(report.Addenda.Select(a => a.AddedBy));
        var names = await db.Set<StaffAccountAggregate>().AsNoTracking().Where(a => ids.Contains(a.Id)).Select(a => new { a.Id, a.FullName }).ToDictionaryAsync(a => a.Id, a => a.FullName);
        string Name(Guid id) => names.GetValueOrDefault(id, "");

        var catName = await db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == report.CatId).Select(c => c.Name).FirstOrDefaultAsync() ?? "";
        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == report.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";

        return new QueryResponse<SurgicalReportViewModel?>
        {
            Result = new SurgicalReportViewModel(
                report.Id, report.CatId, catName, report.VisitId, report.BranchId, timeZoneId, report.Version, report.FinalisedOn is null ? "Draft" : "Finalised", report.ProcedurePerformed,
                report.Findings, report.Technique, report.SutureMaterial, report.HadComplications, report.Complications, report.SurgeonId, Name(report.SurgeonId), report.AssistantId,
                Name(report.AssistantId), report.FinalisedOn, report.FinalisedBy, report.FinalisedBy is { } fb ? Name(fb) : null,
                report.Addenda.Select(a => new SurgicalReportAddendumViewModel(a.Id, a.Text, a.AddedBy, Name(a.AddedBy), a.AddedOn)).ToList())
        };
    }
}
