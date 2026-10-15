using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Boarding.BoardingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Boarding;

/// <summary>C20 -- US-BRD-14. A boarded cat is sent to the clinic. The stay raises <c>BoardedCatReferredToClinicEvent</c>; HO-EMR-001 opens the visit and answers with <c>ReferralVisitOpenedEvent</c>.</summary>
public sealed class ReferStayCatToClinicCommandHandler(IAggregateRootRepository<BoardingStayAggregate> stayRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<ReferStayCatToClinicCommand>
{
    public async Task<CommandResponse> HandleAsync(ReferStayCatToClinicCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        CatEcommerce.Domain.Entities.VetReferral referral;
        try
        {
            referral = stay.ReferCat(new ReferDto(command.CatId, command.Reason, BoardingUnitMapper.ParseEnum<TriagePriority>(command.Urgency), timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success(referral.Id);
    }
}

public sealed class RecordReferralReturnCommandHandler(IAggregateRootRepository<BoardingStayAggregate> stayRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordReferralReturnCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordReferralReturnCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        try
        {
            stay.ReturnCatFromClinic(command.ReferralId, timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C20 -- a charge above the cat's headroom needs the owner's approval. The urgent message is enqueued before the save; the treatment is held by HO-EMR-001 until the decision event.</summary>
public sealed class RequestVetChargeApprovalCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository, INotificationOutbox notificationOutbox, INotificationLinkBuilder linkBuilder, TimeProvider timeProvider)
    : ICommandHandlerAsync<RequestVetChargeApprovalCommand>
{
    public async Task<CommandResponse> HandleAsync(RequestVetChargeApprovalCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        CatEcommerce.Domain.Entities.VetChargeApproval approval;
        try
        {
            approval = stay.RequestVetApproval(new RequestVetApprovalDto(command.CatId, command.ReferralId, command.Amount, command.Description, timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        BoardingNotifications.EnqueueVetApprovalRequested(
            notificationOutbox, linkBuilder, stay, stay.FindCat(command.CatId).CatName, approval.Id, approval.Amount, command.CorrelationId, command.UserContext!);
        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success(approval.Id);
    }
}

/// <summary>C20 -- the owner answers on the portal. Someone else's stay is not found (404); an answered request is final.</summary>
public sealed class RespondToVetChargeApprovalCommandHandler(IAggregateRootRepository<BoardingStayAggregate> stayRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<RespondToVetChargeApprovalCommand>
{
    public async Task<CommandResponse> HandleAsync(RespondToVetChargeApprovalCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, command.CustomerId, null);
        if (stay is null) return StayAccess.NotFound();

        try
        {
            stay.DecideVetApproval(new DecideVetApprovalDto(command.ApprovalId, command.Approve, command.Note, AcknowledgementChannel.Portal, timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(BoardingStayAggregate));
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C20 -- staff record an answer given by phone: a note is required and the answer is attributed to the staff member.</summary>
public sealed class RecordVetChargeApprovalByStaffCommandHandler(IAggregateRootRepository<BoardingStayAggregate> stayRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordVetChargeApprovalByStaffCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordVetChargeApprovalByStaffCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        try
        {
            stay.DecideVetApproval(new DecideVetApprovalDto(command.ApprovalId, command.Approve, command.Note, AcknowledgementChannel.Staff, timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(BoardingStayAggregate));
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
