using System.Globalization;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Pharmacy;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Prescription;

/// <summary>
/// HO-PHM-001 §12.6 (US-PHM-02, US-PHM-03) -- the safety check. It loads through the SAME <see cref="ISafetyInputLoader"/> and calls the SAME pure
/// <see cref="PrescriptionSafetyAssessor"/> the issue command runs, and it WRITES NOTHING: it is a query. An unknown cat or medicine yields <c>null</c>.
/// </summary>
public sealed class CheckPrescriptionSafetyQueryHandler(
    CatEcommerceDbContext db, IFormularyReader formulary, ISafetyInputLoader loader, IPharmacyClock clock)
    : IQueryHandlerAsync<CheckPrescriptionSafetyQuery, SafetyAssessmentViewModel?>
{
    public async Task<QueryResponse<SafetyAssessmentViewModel?>> HandleAsync(CheckPrescriptionSafetyQuery query)
    {
        var ct = CancellationToken.None;
        var catName = await db.Cats.AsNoTracking().Where(c => c.Id == query.CatId).Select(c => c.Name).FirstOrDefaultAsync(ct);
        var medicine = await formulary.GetAsync(query.MedicineId, ct);
        if (catName is null || medicine is null) return new QueryResponse<SafetyAssessmentViewModel?> { Result = null };

        if (!EnumerationParsing.TryParse<DoseUnit>(query.DoseUnit, out var unit) || unit is null
            || !EnumerationParsing.TryParse<AdministrationRoute>(query.Route, out var route) || route is null
            || !EnumerationParsing.TryParse<DoseFrequency>(query.Frequency, out var frequency) || frequency is null)
            return new QueryResponse<SafetyAssessmentViewModel?> { Result = null };

        var dose = new Dose(query.DoseAmount, unit);
        var today = await clock.LocalTodayAsync(query.BranchId, ct);
        var input = await loader.LoadAsync(query.CatId, catName, medicine, dose, route, frequency, today, ct);

        return new QueryResponse<SafetyAssessmentViewModel?> { Result = SafetyAssessmentViewModel.From(PrescriptionSafetyAssessor.Assess(input)) };
    }
}

/// <summary>A cat's prescriptions. <c>active</c> keeps those <see cref="PrescriptionActivityRules"/> calls active (completed-but-still-running included, cancelled
/// excluded); <c>all</c> keeps everything. A cat has a bounded number of prescriptions, so the derived filter runs over its rows and the page is cut in memory.</summary>
public sealed class GetCatPrescriptionsQueryHandler(CatEcommerceDbContext db, IPharmacyClock clock)
    : IQueryHandlerAsync<GetCatPrescriptionsQuery, PagedResult<PrescriptionSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<PrescriptionSummaryViewModel>>> HandleAsync(GetCatPrescriptionsQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 100);
        var pageNumber = Math.Max(query.PageNumber, 1);
        var today = await clock.LocalTodayAsync(query.BranchId, CancellationToken.None);

        var rows = await PrescriptionReads.Rows(db, db.Prescriptions.Where(p => p.CatId == query.CatId && !p.IsMarkedToDelete)).ToListAsync();
        var wanted = string.Equals(query.Scope, "all", StringComparison.OrdinalIgnoreCase) ? rows : rows.Where(r => PrescriptionReads.IsActive(r, today)).ToList();

        var page = wanted.OrderByDescending(r => r.IssuedOn).ThenBy(r => r.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(r => PrescriptionReads.ToSummary(r, today)).ToList();

        return new QueryResponse<PagedResult<PrescriptionSummaryViewModel>> { Result = new PagedResult<PrescriptionSummaryViewModel>(page, pageNumber, pageSize, wanted.Count) };
    }
}

public sealed class GetVisitPrescriptionsQueryHandler(CatEcommerceDbContext db, IPharmacyClock clock)
    : IQueryHandlerAsync<GetVisitPrescriptionsQuery, IReadOnlyList<PrescriptionSummaryViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<PrescriptionSummaryViewModel>>> HandleAsync(GetVisitPrescriptionsQuery query)
    {
        var today = await clock.LocalTodayAsync(query.BranchId, CancellationToken.None);
        var rows = await PrescriptionReads.Rows(db, db.Prescriptions.Where(p => p.VisitId == query.VisitId && !p.IsMarkedToDelete)).ToListAsync();

        IReadOnlyList<PrescriptionSummaryViewModel> result = rows.OrderBy(r => r.IssuedOn).Select(r => PrescriptionReads.ToSummary(r, today)).ToList();
        return new QueryResponse<IReadOnlyList<PrescriptionSummaryViewModel>> { Result = result };
    }
}

public sealed class GetCatPrescriptionHistoryQueryHandler(CatEcommerceDbContext db, IPharmacyClock clock)
    : IQueryHandlerAsync<GetCatPrescriptionHistoryQuery, IReadOnlyList<PrescriptionSummaryViewModel>>
{
    private const int MaxHistoryRows = 500;

    public async Task<QueryResponse<IReadOnlyList<PrescriptionSummaryViewModel>>> HandleAsync(GetCatPrescriptionHistoryQuery query)
    {
        var today = await clock.LocalTodayAsync(query.BranchId, CancellationToken.None);
        var rows = await PrescriptionReads.Rows(db, db.Prescriptions.Where(p => p.CatId == query.CatId && !p.IsMarkedToDelete))
            .OrderByDescending(r => r.IssuedOn).Take(MaxHistoryRows).ToListAsync();

        IReadOnlyList<PrescriptionSummaryViewModel> result = rows.Select(r => PrescriptionReads.ToSummary(r, today)).ToList();
        return new QueryResponse<IReadOnlyList<PrescriptionSummaryViewModel>> { Result = result };
    }
}

/// <summary>A prescription in full: the summary, the safety record (what the vet saw and acknowledged), the warnings, the repeat requests, the renewal links and
/// the actions the caller may take now (<see cref="PrescriptionActionRules"/>, shared with the aggregate so the screen never offers a refused button).</summary>
public sealed class GetPrescriptionDetailQueryHandler(CatEcommerceDbContext db, IPharmacyClock clock)
    : IQueryHandlerAsync<GetPrescriptionDetailQuery, PrescriptionDetailViewModel?>
{
    public async Task<QueryResponse<PrescriptionDetailViewModel?>> HandleAsync(GetPrescriptionDetailQuery query)
    {
        var ct = CancellationToken.None;
        var today = await clock.LocalTodayAsync(query.BranchId, ct);

        var row = await PrescriptionReads.Rows(db, db.Prescriptions.Where(p => p.Id == query.PrescriptionId && !p.IsMarkedToDelete)).FirstOrDefaultAsync(ct);
        if (row is null) return new QueryResponse<PrescriptionDetailViewModel?> { Result = null };

        var header = await db.Prescriptions.AsNoTracking().Where(p => p.Id == query.PrescriptionId)
            .Select(p => new
            {
                p.Instructions, p.IsRemoteRenewal, p.RenewedFromPrescriptionId, p.RenewedByPrescriptionId,
                p.SafetyRecord.WeightKg, p.SafetyRecord.WeightRecordedOn, p.SafetyRecord.DoseMgPerKg, p.SafetyRecord.ExpectedMinMgPerKg, p.SafetyRecord.ExpectedMaxMgPerKg,
                p.SafetyRecord.DoseCheckOutcome, p.SafetyRecord.AcknowledgedBy, p.SafetyRecord.AcknowledgedOn, p.SafetyRecord.AllergyAcknowledgementReason,
                p.SafetyRecord.DoseAcknowledgementNote
            })
            .FirstAsync(ct);

        var catName = await db.Cats.AsNoTracking().Where(c => c.Id == row.CatId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "";

        var warnings = await db.Prescriptions.AsNoTracking().Where(p => p.Id == query.PrescriptionId)
            .SelectMany(p => p.Warnings.Select(w => new { w.Kind, w.Blocking, w.RequiresAcknowledgement, w.Severity, w.Subject, w.Detail }))
            .ToListAsync(ct);

        var requests = await db.Prescriptions.AsNoTracking().Where(p => p.Id == query.PrescriptionId)
            .SelectMany(p => p.RepeatRequests.Select(r => new { r.Id, r.RequestedOn, r.PreferredBranchId, r.Note, r.Status, r.DecidedOn, r.DeclineReason, r.DispensingId }))
            .ToListAsync(ct);

        var references = await db.Prescriptions.AsNoTracking()
            .Where(p => p.Id == header.RenewedFromPrescriptionId || p.Id == header.RenewedByPrescriptionId)
            .Select(p => new { p.Id, p.PrescriptionNumber }).ToListAsync(ct);

        var dispensingRows = await db.Dispensings.AsNoTracking().Where(d => d.PrescriptionId == query.PrescriptionId)
            .Select(d => new
            {
                d.Id, d.SupplyNumber, d.IsRepeat, d.DispensedOnLocal, d.DispensedBy, d.Status,
                Lines = d.Lines.Select(l => new { l.Id, l.LotNumber, l.ExpiresOn, l.Quantity }).ToList()
            })
            .ToListAsync(ct);

        var names = await PrescriptionReads.StaffNamesAsync(db, new Guid?[] { header.AcknowledgedBy }.Concat(dispensingRows.Select(d => (Guid?)d.DispensedBy)), ct);

        var effective = PrescriptionReads.EffectiveStatus(row, today);
        var suppliesLeft = PrescriptionActivityRules.SuppliesLeft(row.RepeatsAuthorised, row.SuppliesDispensed);
        var allowed = PrescriptionActionRules.AllowedActions(
            query.CallerRoles, effective, dispensableToday: suppliesLeft > 0 && today <= row.ValidUntil, alreadyRenewed: header.RenewedByPrescriptionId is not null,
            hasOpenRepeatRequest: requests.Any(r => r.Status == RepeatRequestStatus.Requested));

        static string Number(decimal value) => value.ToString("0.00##", CultureInfo.InvariantCulture);

        var summary = PrescriptionReads.ToSummary(row, today);
        var safety = new SafetyRecordViewModel(
            header.WeightKg is { } kg ? QuantityViewModel.Of(kg, "Kg") : null, header.WeightRecordedOn, header.DoseMgPerKg is { } perKg ? Number(perKg) : null,
            header.ExpectedMinMgPerKg is { } min && header.ExpectedMaxMgPerKg is { } max ? new ExpectedRangeViewModel(Number(min), Number(max), null, null) : null,
            header.DoseCheckOutcome.DisplayName, header.AcknowledgedBy is null ? null : PrescriptionReads.NameOf(names, header.AcknowledgedBy), header.AcknowledgedOn,
            header.AllergyAcknowledgementReason, header.DoseAcknowledgementNote);

        PrescriptionReferenceViewModel? Reference(Guid? id) =>
            id is { } value && references.FirstOrDefault(r => r.Id == value) is { } found ? new PrescriptionReferenceViewModel(found.Id, found.PrescriptionNumber) : null;

        var detail = new PrescriptionDetailViewModel(
            summary.Id, summary.PrescriptionNumber, summary.Medicine, summary.Dose, summary.Route, summary.Frequency, summary.DurationDays, summary.Quantity, summary.Status,
            summary.IsActive, summary.CourseEndsOn, summary.RemainingDays, summary.SuppliesDispensed, summary.SuppliesAuthorised, summary.ValidUntil, summary.PrescriberName,
            summary.IssuedOn, summary.HasAcknowledgedWarnings, row.CatId, catName, header.Instructions, row.VisitId, header.IsRemoteRenewal, row.StartsOn, row.IsRepeatable, safety,
            warnings.Select(w => new PrescriptionWarningViewModel(w.Kind.DisplayName, w.Blocking, w.RequiresAcknowledgement, w.Severity?.DisplayName, w.Subject, w.Detail)).ToList(),
            dispensingRows.OrderBy(d => d.SupplyNumber).ThenBy(d => d.DispensedOnLocal).Select(d => new PrescriptionDispensingViewModel(
                d.Id, d.SupplyNumber, d.IsRepeat, d.DispensedOnLocal, PrescriptionReads.NameOf(names, d.DispensedBy), d.Status.DisplayName,
                d.Lines.Select(l => new PrescriptionDispensingLineViewModel(l.Id, l.LotNumber, l.ExpiresOn, QuantityViewModel.Of(l.Quantity, row.DispensingUnit.DisplayName))).ToList())).ToList(),
            requests.OrderBy(r => r.RequestedOn).Select(r => new RepeatRequestViewModel(r.Id, r.RequestedOn, r.PreferredBranchId, r.Note, r.Status.DisplayName, r.DecidedOn, r.DeclineReason, r.DispensingId)).ToList(),
            Reference(header.RenewedFromPrescriptionId), Reference(header.RenewedByPrescriptionId), allowed);

        return new QueryResponse<PrescriptionDetailViewModel?> { Result = detail };
    }
}
