using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Prescription.PharmacyHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Prescription;

/// <summary>The ports the prescribing steps read -- a parameter object, not a service layer (ADR-015). Registered Scoped so both prescribing handlers share it.</summary>
public sealed record PrescribingPorts(
    IAggregateRootRepository<CatAggregate> Cats,
    IAggregateRootRepository<VisitAggregate> Visits,
    IAggregateRootRepository<StaffAccountAggregate> Staff,
    IFormularyReader Formulary,
    ISafetyInputLoader SafetyInputs,
    IPharmacyClock Clock,
    IBusinessSettings Settings,
    IPrescriptionNumberAllocator NumberAllocator,
    TimeProvider TimeProvider);

/// <summary>The outcome of the steps: a complete issue DTO, or the response to return (carrying the assessment when the screen must react to it).</summary>
public sealed record PrescribingOutcome(PrescriptionIssueDto? Issue, CommandResponse? Failure);

/// <summary>
/// HO-PHM-001 §11.4 -- THE one sequence both prescribing handlers (issue C5 and renew C15) run, implemented once so a rule cannot be applied to one and
/// forgotten in the other (a static helper over the ports -- not a service layer):
/// <list type="number">
/// <item>the caller holds <c>Vet</c> (defence in depth behind <c>RequireActionFilter</c>, US-CLN-22);</item>
/// <item>the cat exists and is alive (<c>CatNotTreatable</c>); the visit exists in the active branch, belongs to the cat and is <b>open</b> -- for a renewal only when one is given;</item>
/// <item>the medicine exists, is active and the route is allowed for it;</item>
/// <item>the safety input is loaded (one loader) and assessed (one pure assessor -- the very function the check query runs);</item>
/// <item>a per-kg medicine with no recent weight is refused (<c>RecentWeightRequired</c>) -- no acknowledgement path;</item>
/// <item>the acknowledgement rules of §8.5: not acknowledged -> 400, a stale fingerprint -> 409, an allergy conflict without a reason -> 400;</item>
/// <item>the number is allocated and the DTO built -- the aggregate decides the rest and saves.</item>
/// </list>
/// </summary>
public static class PrescribingSteps
{
    public static async Task<PrescribingOutcome> RunAsync(
        PrescribingPorts ports, PrescribingFieldsCommandBase command, Guid catId, Guid medicineId, Guid? visitId, Guid? remoteRenewalVisitId, Guid? renewedFrom)
    {
        var ct = CancellationToken.None;

        // 1. Vet only.
        var caller = await ports.Staff.GetByIdAsync(command.UserContext!.UserId);
        if (caller is null || !caller.RoleIds.Contains(StaffRoles.Vet))
            return Fail(Violation(BusinessViolationCodes.PrescribingNotPermitted, nameof(command.UserContext), "Only a veterinarian may prescribe."));

        // 2. Cat and visit.
        var cat = await ports.Cats.GetByIdAsync(catId);
        if (cat is null)
            return Fail(Violation(BusinessViolationCodes.CatNotFound, nameof(catId), "Cat not found."));

        if (cat.LifeStatus != CatLifeStatus.Alive)
            return Fail(Violation(BusinessViolationCodes.CatNotTreatable, nameof(catId), $"{cat.Name} is no longer with us, so nothing can be prescribed."));

        var branchId = command.BranchId;
        var isRemote = false;
        var visitForRecord = visitId;
        if (visitId is { } visitToCheck)
        {
            var visit = await ports.Visits.GetByIdAsync(visitToCheck);
            if (visit is null || visit.BranchId != command.BranchId)
                return Fail(Violation(BusinessViolationCodes.VisitNotFound, nameof(visitId), "Visit not found."));
            if (visit.CatId != catId)
                return Fail(Violation(BusinessViolationCodes.VisitCatMismatch, nameof(visitId), "That visit belongs to a different cat."));
            if (visit.Status != VisitStatus.Open)
                return Fail(Violation(BusinessViolationCodes.VisitNotOpen, nameof(visitId), "A prescription can only be written from an open visit."));
        }
        else
        {
            // A remote renewal (OQ-3): no open visit; the record points at the original's visit and is flagged.
            isRemote = true;
            visitForRecord = remoteRenewalVisitId;
        }

        if (visitForRecord is null)
            return Fail(Violation(BusinessViolationCodes.VisitNotFound, nameof(visitId), "A prescription is written from a visit."));

        // 3. Medicine and route.
        var medicine = await ports.Formulary.GetAsync(medicineId, ct);
        if (medicine is null)
            return Fail(Violation(BusinessViolationCodes.FormularyMedicineNotFound, nameof(medicineId), "That medicine is not in the formulary."));
        if (!medicine.IsActive)
            return Fail(Violation(BusinessViolationCodes.MedicineInactive, nameof(medicineId), "That medicine is no longer prescribable."));

        var route = FormularyMapper.ParseEnum<AdministrationRoute>(command.Route);
        var frequency = FormularyMapper.ParseEnum<DoseFrequency>(command.Frequency);
        if (!medicine.AllowedRoutes.Contains(route))
            return Fail(Violation(BusinessViolationCodes.RouteNotAllowedForMedicine, nameof(command.Route), $"{medicine.Name} is not given by that route."));

        Dose dose;
        try { dose = new Dose(command.Dose.Amount, FormularyMapper.ParseEnum<DoseUnit>(command.Dose.Unit)); }
        catch (DomainRuleViolationException ex) { return Fail(Violation(ex)); }

        // 4. Assess -- the very function the check query runs.
        var now = ports.TimeProvider.GetUtcNow();
        var today = await ports.Clock.LocalTodayAsync(branchId, ct);
        var input = await ports.SafetyInputs.LoadAsync(catId, cat.Name, medicine, dose, route, frequency, today, ct, renewedFrom);
        var assessment = PrescriptionSafetyAssessor.Assess(input);

        // 5. Recent weight -- no acknowledgement path (US-PHM-02 AC2).
        if (!assessment.Weight.IsSatisfied)
        {
            var message = assessment.Weight.Kind == WeightRequirementKind.Missing
                ? PharmacyMessages.RecentWeightMissing(cat.Name)
                : PharmacyMessages.RecentWeightStale(cat.Name, assessment.Weight.LatestKilograms!.Value, assessment.Weight.RecordedOn!.Value, assessment.Weight.AgeDays!.Value);
            return Fail(Violation(BusinessViolationCodes.RecentWeightRequired, "Weight", message), assessment);
        }

        // 6. Acknowledgement (§8.5 steps 1-4).
        var acknowledgement = new SafetyAcknowledgementInput(command.AcknowledgedWarningsFingerprint, command.AllergyAcknowledgementReason, command.DoseAcknowledgementNote);
        switch (SafetyAcknowledgement.Evaluate(assessment, acknowledgement))
        {
            case AcknowledgementResult.NotAcknowledged:
                return Fail(Violation(BusinessViolationCodes.PrescriptionWarningsNotAcknowledged, nameof(command.AcknowledgedWarningsFingerprint),
                    "These warnings must be acknowledged before the prescription can be issued."), assessment);
            case AcknowledgementResult.Changed:
                return Fail(Violation(BusinessViolationCodes.PrescriptionWarningsChanged, nameof(command.AcknowledgedWarningsFingerprint),
                    "The warnings have changed since you reviewed them (a new weight, allergy or prescription). Review them again."), assessment);
            case AcknowledgementResult.AllergyReasonRequired:
                return Fail(Violation(BusinessViolationCodes.AllergyAcknowledgementReasonRequired, nameof(command.AllergyAcknowledgementReason),
                    "Explain why a recorded allergy is being overridden."), assessment);
        }

        // 7. The number, and the DTO the aggregate will decide on.
        var startsOn = command.StartsOn ?? today;
        var dto = new PrescriptionIssueDto(
            await ports.NumberAllocator.AllocateAsync(now, ct), branchId, catId, cat.OwnerId, visitForRecord.Value, isRemote, command.UserContext.UserId, medicine, dose, route, frequency,
            command.DurationDays, command.Quantity, command.Instructions, startsOn, command.IsRepeatable, command.RepeatsAuthorised, command.ValidUntil, today,
            ports.Settings.GetInt(SettingKeys.PharmacyDefaultDispenseWindowDays), ports.Settings.GetInt(SettingKeys.PharmacyMaxRepeatValidityDays),
            assessment, acknowledgement, now, renewedFrom, command.ToPharmacyActor());

        return new PrescribingOutcome(dto, null);
    }

    private static PrescribingOutcome Fail(CommandResponse failure, SafetyAssessment? assessment = null)
    {
        if (assessment is not null) failure.Result = assessment;
        return new PrescribingOutcome(null, failure);
    }
}
