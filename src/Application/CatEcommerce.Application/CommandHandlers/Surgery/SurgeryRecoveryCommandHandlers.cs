using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Surgery.SurgeryHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Surgery;

/// <summary>HO-SRG-001 section 11.4 C14 (US-SRG-10, ADR-168) -- set the recovery status. Any of the three to any other; the same status is a no-op; every change is a history row with who and when.</summary>
public sealed class SetRecoveryStatusCommandHandler(IAggregateRootRepository<SurgeryAggregate> surgeryRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<SetRecoveryStatusCommand>
{
    public async Task<CommandResponse> HandleAsync(SetRecoveryStatusCommand command)
    {
        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        try
        {
            surgery.SetRecoveryStatus(new RecoveryStatusDto(
                AppointmentMapper.ParseEnum<RecoveryStatus>(command.Status), command.Note, timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
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
/// HO-SRG-001 section 11.4 C15 (US-SRG-07) -- discharge. Loads the protocol (or its shipped defaults), the cat (alive?) and the branch; the aggregate checks the status, the readiness and
/// every follow-up's window; then the post-op message and ONE reminder per follow-up are enlisted and the surgery is saved ONCE. A DECEASED cat is closed out from any recovery status
/// with no follow-ups, no instructions and no messages (U-F6).
/// </summary>
public sealed class DischargeSurgeryCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAggregateRootRepository<SurgeryProtocolAggregate> protocolRepository,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<DischargeSurgeryCommand>
{
    public async Task<CommandResponse> HandleAsync(DischargeSurgeryCommand command)
    {
        var user = command.UserContext!;
        var now = timeProvider.GetUtcNow();

        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);
        if (!SurgeryTransitionRules.IsAllowed(surgery.Status, SurgeryTransitionRules.Discharge))
            return TransitionViolation(surgery.Status, SurgeryTransitionRules.Discharge);

        var cat = await catRepository.GetByIdAsync(surgery.CatId);
        var branch = await branchRepository.GetByIdAsync(surgery.BranchId);
        if (branch is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);
        var catIsDeceased = cat is not null && cat.LifeStatus == CatLifeStatus.Deceased;

        var (rules, protocolInstructions) = await SurgeryProtocols.DischargeProtocolAsync(protocolRepository, surgery.ClinicServiceId);
        var procedureDate = surgery.ProcedureEndedLocalDate ?? BranchCalendar.LocalDateOf(now, branch.TimeZoneId);
        var plan = FollowUpPlanner.Plan(rules, procedureDate);

        var inputs = new List<PlannedFollowUpInput>();
        if (!catIsDeceased)
        {
            var requested = command.FollowUps ?? plan.Select(p => new DischargeFollowUpInput(p.Kind.DisplayName, p.DueOn)).ToList();
            foreach (var item in requested)
            {
                var kind = AppointmentMapper.ParseEnum<SurgeryFollowUpKind>(item.Kind);
                var planned = plan.FirstOrDefault(p => p.Kind == kind);
                if (planned is null)
                    return Violation(BusinessViolationCodes.FollowUpOutsideWindow, nameof(command.FollowUps), $"This procedure has no {SurgeryNotifications.Describe(kind).ToLowerInvariant()} follow-up.");
                if (inputs.Any(i => i.Kind == kind))
                    return Violation(BusinessViolationCodes.FollowUpOutsideWindow, nameof(command.FollowUps), $"Only one {SurgeryNotifications.Describe(kind).ToLowerInvariant()} follow-up can be set.");

                inputs.Add(new PlannedFollowUpInput(Guid.NewGuid(), kind, item.DueOn, planned.WindowFrom, planned.WindowTo));
            }
        }

        try
        {
            surgery.Discharge(new SurgeryDischargeDto(command.PostOpInstructions ?? protocolInstructions, inputs, catIsDeceased, now, user, command.CorrelationId, cat?.OwnerId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        if (!catIsDeceased)
        {
            var ctx = new SurgeryNotifications.MessageContext(cat?.Name ?? "your cat", branch.Name, branch.AddressLine ?? "", branch.TimeZoneId);
            SurgeryNotifications.EnqueuePostOp(notificationOutbox, linkBuilder, surgery, ctx, command.CorrelationId, user);
            foreach (var followUp in surgery.FollowUps)
                SurgeryNotifications.EnqueueFollowUpReminder(notificationOutbox, linkBuilder, businessSettings, surgery, followUp, ctx, now, command.CorrelationId, user);
        }

        await surgeryRepository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C15 -- complete a follow-up (the owner came). Closing it makes its pending reminder fail the <c>SurgeryFollowUp</c> guard.</summary>
public sealed class CompleteSurgeryFollowUpCommandHandler(IAggregateRootRepository<SurgeryAggregate> surgeryRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<CompleteSurgeryFollowUpCommand>
{
    public async Task<CommandResponse> HandleAsync(CompleteSurgeryFollowUpCommand command) =>
        await SurgeryFollowUpClosing.RunAsync(surgeryRepository, command.SurgeryId, command.BranchId,
            new FollowUpCloseDto(command.FollowUpId, FollowUpStatus.Done, null, timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
}

/// <summary>C15 -- cancel a follow-up with a reason (for example no longer needed).</summary>
public sealed class CancelSurgeryFollowUpCommandHandler(IAggregateRootRepository<SurgeryAggregate> surgeryRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<CancelSurgeryFollowUpCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelSurgeryFollowUpCommand command) =>
        await SurgeryFollowUpClosing.RunAsync(surgeryRepository, command.SurgeryId, command.BranchId,
            new FollowUpCloseDto(command.FollowUpId, FollowUpStatus.Cancelled, command.Reason, timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
}

internal static class SurgeryFollowUpClosing
{
    public static async Task<CommandResponse> RunAsync(IAggregateRootRepository<SurgeryAggregate> repository, Guid surgeryId, Guid branchId, FollowUpCloseDto dto)
    {
        var surgery = await LoadSurgeryAsync(repository, surgeryId, branchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, "SurgeryId", SurgeryMessages.SurgeryNotFound);

        try
        {
            surgery.CloseFollowUp(dto);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await repository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
