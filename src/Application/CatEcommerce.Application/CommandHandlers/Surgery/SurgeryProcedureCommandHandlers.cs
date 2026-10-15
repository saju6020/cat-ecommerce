using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Application.DomainEventHandlers.Cat;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Surgery.SurgeryHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Surgery;

/// <summary>
/// HO-SRG-001 §11.4 C11 (US-SRG-05, US-SRG-06, ADR-158) -- end the procedure. The surgery goes to Recovery (<c>Recovering</c>) and its appointment to Completed, freeing any planned time that
/// is left, in ONE save: the recorded surgery-appointment exception. A Completed spay or neuter raises <c>SterilisationCompletedEvent</c>; the Cat context handles it AFTER this commit.
/// The report is NOT created here (C12 creates the draft on first save), which keeps this handler inside the recorded surgery-appointment pair.
/// </summary>
public sealed class EndSurgicalProcedureCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    ISurgeryClock clock,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<EndSurgicalProcedureCommand>
{
    public async Task<CommandResponse> HandleAsync(EndSurgicalProcedureCommand command)
    {
        var user = command.UserContext!;
        var now = timeProvider.GetUtcNow();

        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        if (!await IsVetAsync(staffRepository, user.UserId))
            return Violation(BusinessViolationCodes.VeterinarianRequired, nameof(command.UserContext), "Only a veterinarian ends a surgical procedure.");

        if (!SurgeryTransitionRules.IsAllowed(surgery.Status, SurgeryTransitionRules.EndProcedure))
            return TransitionViolation(surgery.Status, SurgeryTransitionRules.EndProcedure);

        var appointment = await appointmentRepository.GetByIdAsync(surgery.AppointmentId);
        if (appointment is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        try
        {
            surgery.EndProcedure(new SurgeryEndProcedureDto(
                AppointmentMapper.ParseEnum<ProcedureOutcome>(command.Outcome), now, await clock.TimeZoneIdAsync(surgery.BranchId, CancellationToken.None), user, command.CorrelationId));
            appointment.CompleteSurgerySlot(new SurgeryAppointmentCompleteDto(surgery.Id, now, user));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        // Saving the surgery commits the modified appointment with it (F23); its events (the sterilisation event among them) dispatch after the commit.
        await surgeryRepository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>HO-SRG-001 §11.4 C12 (US-SRG-05, ADR-165) -- save the DRAFT report. Allowed once the procedure has ended (Recovery or Discharged), by a vet. The first save creates the draft (the
/// surgery id IS the report id); later saves carry <c>ExpectedVersion</c>. After finalisation: <c>ReportNotEditable</c> (409).</summary>
public sealed class SaveSurgicalReportDraftCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<SurgicalReportAggregate> reportRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository)
    : ICommandHandlerAsync<SaveSurgicalReportDraftCommand>
{
    public async Task<CommandResponse> HandleAsync(SaveSurgicalReportDraftCommand command)
    {
        var user = command.UserContext!;

        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        if (!await IsVetAsync(staffRepository, user.UserId))
            return Violation(BusinessViolationCodes.VeterinarianRequired, nameof(command.UserContext), "Only a veterinarian writes the surgical report.");

        if (surgery.Status != SurgeryStatus.Recovery && surgery.Status != SurgeryStatus.Discharged)
            return Violation(BusinessViolationCodes.InvalidSurgeryTransition, nameof(command.SurgeryId), $"The report can be written once the procedure has ended; this surgery is {surgery.Status.DisplayName.ToLowerInvariant()}.", "SurgicalReportAggregate");

        foreach (var staffId in new[] { command.SurgeonId, command.AssistantId })
        {
            if (await staffRepository.GetByIdAsync(staffId) is null)
                return Violation(BusinessViolationCodes.SurgeryTextInvalid, staffId == command.SurgeonId ? nameof(command.SurgeonId) : nameof(command.AssistantId), "That staff member does not exist.", "SurgicalReportAggregate");
        }

        var report = await reportRepository.GetByIdAsync(surgery.Id);
        var isNew = report is null;
        report ??= new SurgicalReportAggregate(surgery.Id);

        if (!isNew)
        {
            if (report.IsFinalised)
                return Violation(BusinessViolationCodes.ReportNotEditable, nameof(command.SurgeryId), "This report has been finalised and can no longer be edited. Add an addendum instead.", "SurgicalReportAggregate");
            if (command.ExpectedVersion is not { } expected || report.Version != expected)
                return Violation(BusinessViolationCodes.StaleVersion, nameof(command.ExpectedVersion), "The report has changed since you loaded it. Reload and try again.", "SurgicalReportAggregate");
        }

        try
        {
            report.SaveDraft(new SurgicalReportDraftDto(
                surgery.CatId, surgery.VisitId, surgery.BranchId, command.ProcedurePerformed, command.Findings, command.Technique, command.SutureMaterial, command.HadComplications,
                command.Complications, command.SurgeonId, command.AssistantId, user, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex, "SurgicalReportAggregate");
        }

        if (isNew) await reportRepository.SaveAsync(report, EventPublishOption.InMemoryPublishOnly);
        else await reportRepository.UpdateAsync(report, EventPublishOption.InMemoryPublishOnly);

        return Success(new SurgicalReportSavedResult(report.Version));
    }
}

public sealed record SurgicalReportSavedResult(int Version);

/// <summary>HO-SRG-001 §11.4 C12 (BR-6, BR-11, D-5) -- finalise (vet only): every required field present, then immutable.</summary>
public sealed class FinaliseSurgicalReportCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<SurgicalReportAggregate> reportRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<FinaliseSurgicalReportCommand>
{
    public async Task<CommandResponse> HandleAsync(FinaliseSurgicalReportCommand command)
    {
        var user = command.UserContext!;

        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        var report = surgery is null ? null : await reportRepository.GetByIdAsync(surgery.Id);
        if (surgery is null || report is null)
            return Violation(BusinessViolationCodes.SurgicalReportNotFound, nameof(command.SurgeryId), "Surgical report not found.", "SurgicalReportAggregate");

        if (report.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleVersion, nameof(command.ExpectedVersion), "The report has changed since you loaded it. Reload and try again.", "SurgicalReportAggregate");

        try
        {
            report.Finalise(new SurgicalReportFinaliseDto(await IsVetAsync(staffRepository, user.UserId), timeProvider.GetUtcNow(), user, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex, "SurgicalReportAggregate");
        }

        await reportRepository.UpdateAsync(report, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>HO-SRG-001 §11.4 C12 -- an addendum to a FINALISED report: vet only, append-only. Saves only the addendum row (the report row is never updated once finalised).</summary>
public sealed class AddSurgicalReportAddendumCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<SurgicalReportAggregate> reportRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AddSurgicalReportAddendumCommand>
{
    public async Task<CommandResponse> HandleAsync(AddSurgicalReportAddendumCommand command)
    {
        var user = command.UserContext!;

        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        var report = surgery is null ? null : await reportRepository.GetByIdAsync(surgery.Id);
        if (surgery is null || report is null)
            return Violation(BusinessViolationCodes.SurgicalReportNotFound, nameof(command.SurgeryId), "Surgical report not found.", "SurgicalReportAggregate");

        try
        {
            report.AddAddendum(new SurgicalReportAddendumDto(command.AddendumId, command.Text, await IsVetAsync(staffRepository, user.UserId), timeProvider.GetUtcNow(), user, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex, "SurgicalReportAggregate");
        }

        await reportRepository.UpdateAsync(report, EventPublishOption.InMemoryPublishOnly);
        return Success(command.AddendumId);
    }
}

/// <summary>
/// HO-SRG-001 §8.14 / §11.4 C13 (US-SRG-06, ADR-166) -- the REPAIR path for the neuter status, for when the event handler could not apply it (a failure after the commit, or a cat that was
/// wrongly marked deceased and corrected). Idempotent. The surgery context never writes the cat: the Cat-context helper does, exactly as the event handler does. A deceased cat is refused
/// plainly (<c>CatIsDeceased</c>).
/// </summary>
public sealed class ApplySterilisationOutcomeCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    ICatRecordClock catClock)
    : ICommandHandlerAsync<ApplySterilisationOutcomeCommand>
{
    public async Task<CommandResponse> HandleAsync(ApplySterilisationOutcomeCommand command)
    {
        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        if (!await IsVetAsync(staffRepository, command.UserContext!.UserId))
            return Violation(BusinessViolationCodes.VeterinarianRequired, nameof(command.UserContext), "Only a veterinarian applies a sterilisation outcome to the cat.");

        if (!surgery.Procedure.IsSterilisation || surgery.Outcome != ProcedureOutcome.Completed || surgery.ProcedureEndedLocalDate is not { } completedOn)
            return Violation(BusinessViolationCodes.InvalidSurgeryTransition, nameof(command.SurgeryId), "Only a completed spay or neuter sets the cat's neuter status.");

        var result = await NeuterStatusApplication.ApplyAsync(catRepository, catClock, surgery.CatId, completedOn, command.UserContext!, command.CorrelationId);
        return result.Outcome is NeuterStatusOutcome.Applied or NeuterStatusOutcome.AlreadyApplied
            ? Success()
            : Violation(result.ViolationCode ?? BusinessViolationCodes.InvalidSurgeryTransition, nameof(command.SurgeryId), result.Message ?? "The neuter status could not be applied.");
    }
}
