using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Visit;

using static VisitCommandHandlerSupport;

/// <summary>
/// C5 -- opens the visit of a checked-in appointment (§11.4). <b>Idempotent:</b> an existing visit for the
/// appointment is returned as success, so two nurses clicking at once get the same visit; a true race is
/// settled by the unique index <c>ix_visit_appointment</c>, which <c>UniqueConstraintExceptionFilter</c>
/// turns into the same answer (§12.5). The branch comes from the appointment and must equal the caller's
/// active branch -- another branch's appointment is "not found" (backlog #8).
/// </summary>
public sealed class OpenVisitCommandHandler(
    IAggregateRootRepository<VisitAggregate> visitRepository,
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<OpenVisitCommand>
{
    public async Task<CommandResponse> HandleAsync(OpenVisitCommand command)
    {
        var appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId);
        if (appointment is null || appointment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.AppointmentNotFound, nameof(command.AppointmentId), "Appointment not found.");

        var existing = await visitRepository.GetByFilterAsync(v => v.AppointmentId == command.AppointmentId);
        if (existing is not null)
            return Success(new OpenVisitResult(existing.Id, AlreadyExisted: true));

        var cat = await catRepository.GetByIdAsync(appointment.CatId);
        if (cat is null)
            return Violation(BusinessViolationCodes.CatNotFound, nameof(appointment.CatId), "Cat not found.");

        var visit = new VisitAggregate(command.VisitId);
        try
        {
            visit.Open(new VisitOpeningDto(
                appointment.BranchId, appointment.Id, appointment.CatId, appointment.Status, cat.LifeStatus,
                appointment.ClinicServiceId, appointment.ServiceSnapshot.Name, appointment.ServiceSnapshot.Category,
                timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await visitRepository.SaveAsync(visit, EventPublishOption.InMemoryPublishOnly);
        return Success(new OpenVisitResult(visit.Id, AlreadyExisted: false));
    }
}

/// <summary>
/// C6 -- records one set of vital signs (§8.3). When a weight is present the cat's weight record is created
/// in the <b>same commit</b> with <c>Id = VitalsEntry.Id</c> (ADR-121, a recorded rule-1 exception).
///
/// <b>Companion commit (F23, ADR-121).</b> The visit is loaded through the repository, so it is tracked by
/// the scoped <c>DbContext</c>. The sequence is: (1) mutate the visit in memory -- every range and
/// confirmation rule runs here, before anything is written; (2) <c>SaveAsync</c> the new weight record:
/// per F23 that one <c>SaveChangesAsync</c> commits <i>every tracked change in the scope</i>, so the visit's
/// new vitals row, the weight row and the visit's <c>xmin</c> check are all one statement batch in one
/// transaction; (3) <c>UpdateAsync</c> the visit, which finds nothing left to write and dispatches the
/// visit's events. If the weight insert fails, neither row is committed (I-B6). No event handler is used:
/// one that failed after commit would leave the weight history disagreeing with the vitals that produced it.
/// </summary>
public sealed class RecordVitalsCommandHandler(
    IAggregateRootRepository<VisitAggregate> visitRepository,
    IAggregateRootRepository<CatWeightRecordAggregate> weightRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordVitalsCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordVitalsCommand command)
    {
        var visit = await visitRepository.GetByIdAsync(command.VisitId);
        if (visit is null || visit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.VisitNotFound, nameof(command.VisitId), ClinicalMessages.VisitNotFound);

        var now = timeProvider.GetUtcNow();
        var dto = command.ToDomainDto(now);

        try
        {
            visit.RecordVitals(dto);
        }
        catch (DomainRuleViolationException ex)
        {
            var failure = Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
            if (ex.ErrorCode == BusinessViolationCodes.VitalsOutsidePlausibleRange)
                failure.Result = UnconfirmedSigns(dto);
            return failure;
        }

        if (dto.WeightKg is { } weightKg)
        {
            var branch = await branchRepository.GetByIdAsync(visit.BranchId);
            var timeZoneId = branch?.TimeZoneId ?? "UTC";

            var weight = new CatWeightRecordAggregate(dto.VitalsEntryId);
            try
            {
                weight.Record(new CatWeightRecordDto(
                    visit.CatId, weightKg, BranchCalendar.LocalDateOf(dto.MeasuredOn, timeZoneId), dto.RecordedBy, null,
                    BranchCalendar.LocalDateOf(now, timeZoneId), command.UserContext, command.CorrelationId));
            }
            catch (DomainRuleViolationException ex)
            {
                return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
            }

            // F23 / ADR-121: this save commits the tracked visit change with the new weight row.
            await weightRepository.SaveAsync(weight, EventPublishOption.InMemoryPublishOnly);
        }

        // Nothing left to write when a weight was saved above (the commit already carried the visit's change);
        // otherwise this is the visit's own commit. Either way it dispatches the visit's events.
        await visitRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);
        return Success(dto.VitalsEntryId);
    }

    /// <summary>The signs that fell outside the plausible range and were not confirmed, with their ranges
    /// (§8.3 "outOfRange").</summary>
    private static IReadOnlyList<OutOfRangeSign> UnconfirmedSigns(VitalsDto dto)
    {
        var values = new Dictionary<VitalSign, decimal>();
        if (dto.WeightKg is { } weight) values[VitalSign.Weight] = weight;
        if (dto.TemperatureC is { } temperature) values[VitalSign.Temperature] = temperature;
        if (dto.HeartRateBpm is { } heartRate) values[VitalSign.HeartRate] = heartRate;
        if (dto.RespiratoryRatePerMin is { } respiratoryRate) values[VitalSign.RespiratoryRate] = respiratoryRate;
        if (dto.BodyConditionScore is { } bodyCondition) values[VitalSign.BodyConditionScore] = bodyCondition;
        if (dto.PainScore is { } pain) values[VitalSign.PainScore] = pain;

        return VitalRangeCheck.Evaluate(values).PlausibleWarnings
            .Where(sign => !dto.ConfirmedOutOfRange.Contains(sign))
            .Select(sign =>
            {
                var range = VitalSignRules.For(sign);
                return new OutOfRangeSign(
                    sign.DisplayName, VitalSignRules.Format(sign, values[sign]), range.Unit,
                    VitalSignRules.Format(sign, range.PlausibleMin!.Value), VitalSignRules.Format(sign, range.PlausibleMax!.Value));
            })
            .ToList();
    }
}
