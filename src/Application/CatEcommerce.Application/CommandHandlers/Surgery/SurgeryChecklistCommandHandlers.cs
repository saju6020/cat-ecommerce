using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
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

/// <summary>HO-SRG-001 §11.4 C5 (US-SRG-02) -- a nurse or vet completes a MANUAL item (fasting instructions issued, pre-anaesthetic blood work). Weight completes from the cat's
/// weight records: by event, or here with a <c>WeightRecordId</c> the handler verifies belongs to the cat and falls inside the window. Consent and the ASA grade have their own paths.</summary>
public sealed class CompleteChecklistItemCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<CatWeightRecordAggregate> weightRepository,
    ISurgeryClock clock,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CompleteChecklistItemCommand>
{
    public async Task<CommandResponse> HandleAsync(CompleteChecklistItemCommand command)
    {
        var ct = CancellationToken.None;
        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        var kind = AppointmentMapper.ParseEnum<PreOpChecklistItemKind>(command.Kind);
        Guid? referenceId = null;
        DateOnly? referenceDate = null;

        if (kind == PreOpChecklistItemKind.WeightRecorded)
        {
            var weight = command.WeightRecordId is { } weightId ? await weightRepository.GetByIdAsync(weightId) : null;
            var surgeryDate = await clock.LocalDateOfAsync(surgery.BranchId, surgery.StartsOn, ct);
            var validityDays = settings.GetInt(SettingKeys.SurgeryWeightValidityDays, surgery.BranchId);

            if (weight is null || weight.CatId != surgery.CatId || !PreOpChecklistRules.IsWeightInWindow(weight.RecordedOn, surgeryDate, validityDays))
                return Violation(BusinessViolationCodes.WeightRecordNotValidForSurgery, nameof(command.WeightRecordId),
                    "Choose a weight record of this cat taken on the day of surgery or shortly before it.");

            referenceId = weight.Id;
            referenceDate = weight.RecordedOn;
        }

        try
        {
            surgery.CompleteChecklistItem(new ChecklistCompleteDto(kind, command.Note, referenceId, referenceDate, timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await surgeryRepository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C5 -- waive the pre-anaesthetic blood work (vet only, reason required). The role is checked again here from the staff account (defence in depth behind <c>Surgery.AssessRisk</c>).</summary>
public sealed class WaiveChecklistItemCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<WaiveChecklistItemCommand>
{
    public async Task<CommandResponse> HandleAsync(WaiveChecklistItemCommand command)
    {
        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        try
        {
            surgery.WaiveChecklistItem(new ChecklistWaiveDto(
                AppointmentMapper.ParseEnum<PreOpChecklistItemKind>(command.Kind), command.Reason, await IsVetAsync(staffRepository, command.UserContext!.UserId),
                timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await surgeryRepository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C5 -- a vet records the ASA grade (I to V, E as a flag).</summary>
public sealed class RecordAsaGradeCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordAsaGradeCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordAsaGradeCommand command)
    {
        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        try
        {
            surgery.RecordAsaGrade(new AsaGradeDto(
                AppointmentMapper.ParseEnum<AsaGrade>(command.Grade), command.IsEmergency, await IsVetAsync(staffRepository, command.UserContext!.UserId),
                timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await surgeryRepository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C6 (US-SRG-03) -- admit: the cat arrives and the surgery is PreOp. On the surgery's local date, or up to <c>surgery.earlyAdmissionHours</c> before it.</summary>
public sealed class AdmitSurgeryCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    ISurgeryClock clock,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AdmitSurgeryCommand>
{
    public async Task<CommandResponse> HandleAsync(AdmitSurgeryCommand command)
    {
        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        try
        {
            surgery.Admit(new SurgeryAdmitDto(
                timeProvider.GetUtcNow(), await clock.TimeZoneIdAsync(surgery.BranchId, CancellationToken.None),
                settings.GetInt(SettingKeys.SurgeryEarlyAdmissionHours, surgery.BranchId), command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await surgeryRepository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// HO-SRG-001 §8.7 / §11.4 C7 (US-SRG-03, BR-5, ADR-161) -- start. Consent is RE-READ from the consent records (the checklist flag is not consulted): a consent withdrawn a minute
/// ago must stop the start even if the handler that reverts the flag has not run. Not recorded and no override: <c>ConsentNotRecorded</c>. An override needs a vet (checked here
/// from the staff account, and by <c>[RequireAction(Surgery.OverrideConsent)]</c> on the endpoint) and a 10 to 1000 character reason, and is stored on the aggregate for good.
/// </summary>
public sealed class StartSurgeryCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    ISurgeryConsentReader consentReader,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<StartSurgeryCommand>
{
    public async Task<CommandResponse> HandleAsync(StartSurgeryCommand command)
    {
        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        // The transition is judged first, so a surgery that is not PreOp says so rather than reporting consent.
        if (!SurgeryTransitionRules.IsAllowed(surgery.Status, SurgeryTransitionRules.Start))
            return TransitionViolation(surgery.Status, SurgeryTransitionRules.Start);

        var coverage = await consentReader.GetCoverageAsync(surgery.CatId, surgery.AppointmentId);

        try
        {
            surgery.Start(new SurgeryStartDto(
                coverage.Covered, command.ConsentOverrideReason, await IsVetAsync(staffRepository, command.UserContext!.UserId),
                timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await surgeryRepository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);

        // Consent was recorded, so an override sent anyway was ignored -- the response says so (§8.7).
        return Success(new StartSurgeryResult(surgery.ConsentOverride is not null, coverage.Covered && command.ConsentOverrideReason is not null));
    }
}

/// <summary>The controller-facing result of C7: whether the start used the override, and whether an override that was sent was ignored because consent was on record.</summary>
public sealed record StartSurgeryResult(bool ConsentWasOverridden, bool OverrideIgnored);
