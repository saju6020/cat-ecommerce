using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Surgery;

/// <summary>
/// HO-SRG-001 §12.4/§12.6 -- the surgery detail: everything in §8.2, the checklist items with who and when, <c>consentRecorded</c> read LIVE from the consent records (not the checklist
/// flag), <c>startBlockers[]</c> (what a start would return now), <c>allowedTransitions[]</c> from the same <c>SurgeryTransitionRules</c> the aggregate uses, the histories, the follow-ups,
/// the price snapshot, the report status, whether the neuter status was applied (sterilisations only) and the cat's latest weight. The surgery is read with a projection; the
/// aggregate is never rehydrated (.ai/read-side-pattern.md section 4). Another branch's surgery is "not found".
/// </summary>
public sealed class GetSurgeryDetailQueryHandler(CatEcommerceDbContext db, ISurgeryConsentReader consentReader)
    : IQueryHandlerAsync<GetSurgeryDetailQuery, SurgeryDetailViewModel?>
{
    public async Task<QueryResponse<SurgeryDetailViewModel?>> HandleAsync(GetSurgeryDetailQuery query)
    {
        var s = await db.Surgeries.AsNoTracking()
            .Where(x => x.Id == query.SurgeryId && x.BranchId == query.BranchId && !x.IsMarkedToDelete)
            .Select(x => new
            {
                x.Id, x.Version, x.BranchId, x.Status, x.CatId, x.CustomerId, x.VisitId, x.AppointmentId, x.TheatreId, x.SurgeonId, x.AssistantId,
                x.StartsOn, x.EndsOn, x.AdmitBy, x.FastingInstructions, x.AsaGrade, x.AsaIsEmergency, x.AdmittedOn, x.ProcedureStartedOn, x.ProcedureEndedOn,
                x.ProcedureEndedLocalDate, x.DischargedOn, x.Outcome, x.RecoveryStatus, x.PostOpInstructions, x.CancelledOn, x.CancellationReason,
                ProcedureName = x.Procedure.ServiceName, ProcedureCategory = x.Procedure.Category, x.Procedure.DurationMinutes, x.Procedure.IsSterilisation,
                PriceAmount = x.PriceSnapshot.Amount.Amount, PriceCurrency = x.PriceSnapshot.Amount.CurrencyCode, PriceBasis = x.PriceSnapshot.Basis,
                x.PriceSnapshot.CampaignId, x.PriceSnapshot.CampaignEligibilityNote,
                OverrideBy = x.ConsentOverride != null ? (Guid?)x.ConsentOverride.OverriddenBy : null,
                OverrideOn = x.ConsentOverride != null ? (DateTimeOffset?)x.ConsentOverride.OverriddenOn : null,
                OverrideReason = x.ConsentOverride != null ? x.ConsentOverride.Reason : null,
                Checklist = x.Checklist.Select(i => new { i.Id, i.Kind, i.Status, i.CompletedBy, i.CompletedOn, i.Note, i.WaiverReason, i.ReferenceId }).ToList(),
                StatusHistory = x.StatusHistory.OrderBy(h => h.Sequence).Select(h => new { h.From, h.To, h.ChangedOn, h.ChangedBy }).ToList(),
                RecoveryHistory = x.RecoveryHistory.OrderBy(h => h.Sequence).Select(h => new { h.From, h.To, h.ChangedOn, h.ChangedBy, h.Note }).ToList(),
                FollowUps = x.FollowUps.OrderBy(f => f.DueOn).Select(f => new { f.Id, f.Kind, f.DueOn, f.WindowFrom, f.WindowTo, f.Status, f.ClosedOn }).ToList(),
            })
            .FirstOrDefaultAsync();

        if (s is null) return new QueryResponse<SurgeryDetailViewModel?> { Result = null };

        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == s.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var cat = await db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == s.CatId)
            .Select(c => new { c.Name, c.LifeStatus, c.NeuterStatus, c.NeuteredOn }).FirstOrDefaultAsync();
        var customer = await db.Set<CustomerAccountAggregate>().AsNoTracking().Where(c => c.Id == s.CustomerId)
            .Select(c => new { c.FullName, Phone = c.MobileNumber != null ? c.MobileNumber.Value : null }).FirstOrDefaultAsync();
        var theatreName = await db.Set<RoomAggregate>().AsNoTracking().Where(r => r.Id == s.TheatreId).Select(r => r.Name).FirstOrDefaultAsync() ?? "";

        var actorIds = new List<Guid> { s.SurgeonId, s.AssistantId };
        if (s.OverrideBy is { } overrideActor) actorIds.Add(overrideActor);
        actorIds.AddRange(s.RecoveryHistory.Select(h => h.ChangedBy));
        var allStaffIds = actorIds.Distinct().ToList();
        var names = await db.Set<StaffAccountAggregate>().AsNoTracking().Where(a => allStaffIds.Contains(a.Id)).Select(a => new { a.Id, a.FullName }).ToDictionaryAsync(a => a.Id, a => a.FullName);

        var weight = await db.CatWeightRecords.AsNoTracking().Where(w => w.CatId == s.CatId && !w.IsMarkedToDelete)
            .OrderByDescending(w => w.RecordedOn).ThenByDescending(w => w.CreatedDate)
            .Select(w => new { w.Weight.Kilograms, w.RecordedOn }).FirstOrDefaultAsync();

        var finalisedOn = await db.SurgicalReports.AsNoTracking().Where(r => r.Id == s.Id && !r.IsMarkedToDelete).Select(r => new { r.FinalisedOn }).FirstOrDefaultAsync();
        var reportStatus = finalisedOn is null ? "None" : finalisedOn.FinalisedOn is null ? "Draft" : "Finalised";

        var coverage = await consentReader.GetCoverageAsync(s.CatId, s.AppointmentId);

        var checklistEntities = s.Checklist.Select(i => (Kind: i.Kind, Status: i.Status)).ToList();
        var outstandingAll = PreOpChecklistRules.Order
            .Where(k => checklistEntities.Any(i => i.Kind == k && i.Status == ChecklistItemStatus.Outstanding)).ToList();
        var outstandingForStart = outstandingAll.Where(k => k != PreOpChecklistItemKind.ConsentSigned).ToList();

        var blockers = new List<SurgeryStartBlockerViewModel>();
        if (!SurgeryTransitionRules.IsAllowed(s.Status, SurgeryTransitionRules.Start))
        {
            try { SurgeryTransitionRules.EnsureAllowed(s.Status, SurgeryTransitionRules.Start); }
            catch (DomainRuleViolationException ex) { blockers.Add(new SurgeryStartBlockerViewModel(ex.ErrorCode, ex.Message)); }
        }
        else
        {
            if (!coverage.Covered) blockers.Add(new SurgeryStartBlockerViewModel(BusinessViolationCodes.ConsentNotRecorded, SurgeryMessages.ConsentNotRecorded));
            if (outstandingForStart.Count > 0)
                blockers.Add(new SurgeryStartBlockerViewModel(BusinessViolationCodes.PreOpChecklistIncomplete, $"The pre-op checklist is not complete: {PreOpChecklistRules.Describe(outstandingForStart)}."));
        }

        // Sterilisations only (US-SRG-06): was the cat's neuter status set from this surgery? Null for any other procedure, and for a sterilisation that has not been completed.
        bool? neuterApplied = s.IsSterilisation && s.Outcome == ProcedureOutcome.Completed && cat is not null
            ? cat.NeuterStatus == NeuterStatus.Neutered && cat.NeuteredOn == s.ProcedureEndedLocalDate
            : null;

        string Name(Guid id) => names.GetValueOrDefault(id, "");

        var detail = new SurgeryDetailViewModel(
            s.Id, s.Version, s.BranchId, timeZoneId, s.Status.DisplayName, s.CatId, cat?.Name ?? "", cat?.LifeStatus == CatLifeStatus.Deceased, s.CustomerId, customer?.FullName ?? "", customer?.Phone,
            s.VisitId, s.AppointmentId, new SurgeryProcedureViewModel(s.ProcedureName, s.ProcedureCategory.DisplayName, s.DurationMinutes, s.IsSterilisation),
            new SurgeryPriceViewModel(SurgeryReadFormat.Money(s.PriceAmount, s.PriceCurrency), s.PriceBasis.DisplayName, s.CampaignId, s.CampaignEligibilityNote),
            new SurgeryNamedRefViewModel(s.TheatreId, theatreName), new SurgeryNamedRefViewModel(s.SurgeonId, Name(s.SurgeonId)), new SurgeryNamedRefViewModel(s.AssistantId, Name(s.AssistantId)),
            s.StartsOn, s.EndsOn, s.AdmitBy, s.FastingInstructions, s.AsaGrade?.DisplayName, s.AsaIsEmergency,
            s.OverrideBy is { } overriddenBy ? new SurgeryConsentOverrideViewModel(overriddenBy, Name(overriddenBy), s.OverrideOn!.Value, s.OverrideReason!) : null,
            s.AdmittedOn, s.ProcedureStartedOn, s.ProcedureEndedOn, s.DischargedOn, s.Outcome?.DisplayName, s.RecoveryStatus?.DisplayName, s.PostOpInstructions, s.CancelledOn, s.CancellationReason,
            PreOpChecklistRules.Order.Select(k => s.Checklist.First(i => i.Kind == k))
                .Select(i => new SurgeryChecklistItemViewModel(i.Id, i.Kind.DisplayName, i.Status.DisplayName, i.CompletedBy, i.CompletedOn, i.Note, i.WaiverReason, i.ReferenceId)).ToList(),
            new SurgeryChecklistSummaryViewModel(outstandingAll.Count == 0, outstandingAll.Select(k => k.DisplayName).ToList()), coverage.Covered,
            blockers, SurgeryTransitionRules.AllowedActions(s.Status),
            s.StatusHistory.Select(h => new SurgeryStatusChangeViewModel(h.From?.DisplayName, h.To.DisplayName, h.ChangedOn, h.ChangedBy)).ToList(),
            s.RecoveryHistory.Select(h => new SurgeryRecoveryChangeViewModel(h.From?.DisplayName, h.To.DisplayName, h.ChangedOn, h.ChangedBy, Name(h.ChangedBy), h.Note)).ToList(),
            s.FollowUps.Select(f => new SurgeryFollowUpViewModel(f.Id, f.Kind.DisplayName, f.DueOn, f.WindowFrom, f.WindowTo, f.Status.DisplayName, f.ClosedOn)).ToList(),
            reportStatus, neuterApplied, weight is null ? null : new SurgeryLatestWeightViewModel(weight.Kilograms, weight.RecordedOn));

        return new QueryResponse<SurgeryDetailViewModel?> { Result = detail };
    }
}
