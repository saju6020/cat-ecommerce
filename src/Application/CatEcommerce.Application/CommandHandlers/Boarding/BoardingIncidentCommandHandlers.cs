using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Boarding.BoardingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Boarding;

/// <summary>C18 -- US-BRD-12. The stay must be in house (or checked out within a day); the file is a HO-FIL-001 upload of purpose BoardingMedia by the caller, inside the attach window.
/// One file is one diary item (the unique index <c>ix_stay_media_file_id</c>).</summary>
public sealed class AddStayMediaCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<StayMediaAggregate> mediaRepository,
    IAggregateRootRepository<BoardingIncidentAggregate> incidentRepository,
    IAggregateRootRepository<StoredFileAggregate> fileRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AddStayMediaCommand>
{
    public async Task<CommandResponse> HandleAsync(AddStayMediaCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        var now = timeProvider.GetUtcNow();
        var recentlyOut = stay.Status == BoardingStayStatus.CheckedOut && stay.CheckedOutOn is { } outOn && now - outOn <= TimeSpan.FromDays(1);
        if (stay.Status != BoardingStayStatus.CheckedIn && !recentlyOut)
            return Violation(BusinessViolationCodes.MediaStayNotInHouse, nameof(command.StayId), "Photos can only be added while the cat is boarding (or within a day of check-out).");

        if (command.CatId is { } catId && stay.Cats.All(c => c.CatId != catId))
            return Violation(BusinessViolationCodes.InvalidMediaReference, nameof(command.CatId), "That cat is not on this stay.");

        if (command.IncidentId is { } incidentId)
        {
            var incident = await incidentRepository.GetByIdAsync(incidentId);
            if (incident is null || incident.StayId != stay.Id)
                return Violation(BusinessViolationCodes.IncidentNotFound, nameof(command.IncidentId), "Incident not found.");
        }

        var file = await fileRepository.GetByIdAsync(command.FileId);
        if (file is null || !file.IsEligibleForAttach(FilePurpose.BoardingMedia, command.UserContext!.UserId, now, FileRules.AttachWindow))
            return Violation(BusinessViolationCodes.InvalidMediaReference, nameof(command.FileId), "The file could not be attached. Upload it again and try immediately.");

        var media = new StayMediaAggregate(command.MediaId);
        try
        {
            media.Add(stay.BranchId, stay.Id, command.CatId, command.IncidentId, command.FileId, command.Caption, command.CapturedOnLocalDate, now, command.UserContext);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(StayMediaAggregate));
        }

        await mediaRepository.SaveAsync(media, EventPublishOption.InMemoryPublishOnly);
        return Success(media.Id);
    }
}

public sealed class HideStayMediaCommandHandler(IAggregateRootRepository<StayMediaAggregate> mediaRepository)
    : ICommandHandlerAsync<HideStayMediaCommand>
{
    public async Task<CommandResponse> HandleAsync(HideStayMediaCommand command)
    {
        var media = await mediaRepository.GetByIdAsync(command.MediaId);
        if (media is null || media.StayId != command.StayId || media.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.StayMediaNotFound, nameof(command.MediaId), "Media not found.", nameof(StayMediaAggregate));

        try
        {
            media.Hide(command.Reason);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(StayMediaAggregate));
        }

        await mediaRepository.UpdateAsync(media, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C19 -- US-BRD-13. Medium -> <c>boarding.incident-medium</c> (Normal); high -> <c>boarding.incident-high</c> (Urgent, so it bypasses quiet hours and leaves on the next 5-second
/// notification poll -- FR-NTF-7's 15 minutes). Low sends nothing. The message is enqueued before the save. No description or health text is ever in the message.</summary>
public sealed class RecordIncidentCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<BoardingIncidentAggregate> incidentRepository,
    IBoardingInputs boardingInputs,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordIncidentCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordIncidentCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        var cat = stay.Cats.FirstOrDefault(c => c.CatId == command.CatId);
        var now = timeProvider.GetUtcNow();
        var recentlyOut = stay.Status == BoardingStayStatus.CheckedOut && stay.CheckedOutOn is { } outOn && now - outOn <= TimeSpan.FromDays(1);
        if (cat is null || (stay.Status != BoardingStayStatus.CheckedIn && !recentlyOut))
            return Violation(BusinessViolationCodes.IncidentStayNotInHouse, nameof(command.StayId), "An incident can only be recorded for a cat that is currently boarding.");

        var severity = BoardingUnitMapper.ParseEnum<IncidentSeverity>(command.Severity);
        var kind = BoardingUnitMapper.ParseEnum<IncidentKind>(command.Kind);
        var incident = new BoardingIncidentAggregate(command.IncidentId);
        try
        {
            incident.Record(stay.BranchId, stay.Id, cat.CatId, kind, severity, command.Description, command.ImmediateAction, command.OccurredOn, now, command.UserContext!, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(BoardingIncidentAggregate));
        }

        if (severity != IncidentSeverity.Low)
        {
            var zone = (await boardingInputs.GetSettingsAsync(stay.BranchId))?.TimeZoneId ?? "UTC";
            BoardingNotifications.EnqueueIncident(notificationOutbox, linkBuilder, stay, cat.CatName, incident, zone, command.CorrelationId, command.UserContext!);
        }

        await incidentRepository.SaveAsync(incident, EventPublishOption.InMemoryPublishOnly);
        return Success(incident.Id);
    }
}

/// <summary>The owner acknowledges from the portal: their own stay's incident only (another customer's is "not found"). Idempotent.</summary>
public sealed class AcknowledgeIncidentCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository, IAggregateRootRepository<BoardingIncidentAggregate> incidentRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<AcknowledgeIncidentCommand>
{
    public async Task<CommandResponse> HandleAsync(AcknowledgeIncidentCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, command.CustomerId, null);
        var incident = stay is null ? null : await incidentRepository.GetByIdAsync(command.IncidentId);
        // A low incident is internal: the owner is never shown it, so it does not exist for them (SECURITY 2026-10-15).
        if (stay is null || incident is null || incident.StayId != stay.Id || incident.Severity == IncidentSeverity.Low)
            return Violation(BusinessViolationCodes.IncidentNotFound, nameof(command.IncidentId), "Incident not found.", nameof(BoardingIncidentAggregate));

        incident.Acknowledge(timeProvider.GetUtcNow(), AcknowledgementChannel.Portal);
        await incidentRepository.UpdateAsync(incident, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class RecordIncidentAcknowledgementCommandHandler(IAggregateRootRepository<BoardingIncidentAggregate> incidentRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordIncidentAcknowledgementCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordIncidentAcknowledgementCommand command)
    {
        var incident = await incidentRepository.GetByIdAsync(command.IncidentId);
        if (incident is null || incident.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.IncidentNotFound, nameof(command.IncidentId), "Incident not found.", nameof(BoardingIncidentAggregate));

        incident.Acknowledge(timeProvider.GetUtcNow(), AcknowledgementChannel.Staff);
        await incidentRepository.UpdateAsync(incident, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>Worker only (ADR-226): the branch's escalation minutes decide; anything not yet due is a success that writes nothing.</summary>
public sealed class EscalateIncidentCommandHandler(
    IAggregateRootRepository<BoardingIncidentAggregate> incidentRepository, IBusinessSettings settings, TimeProvider timeProvider)
    : ICommandHandlerAsync<EscalateIncidentCommand>
{
    public async Task<CommandResponse> HandleAsync(EscalateIncidentCommand command)
    {
        var incident = await incidentRepository.GetByIdAsync(command.IncidentId);
        if (incident is null) return Success();

        var minutes = settings.GetInt(SettingKeys.BoardingIncidentAckEscalationMinutes, incident.BranchId);
        var before = incident.EscalatedOn;
        incident.Escalate(timeProvider.GetUtcNow(), minutes, command.UserContext!, command.CorrelationId);
        if (incident.EscalatedOn == before) return Success();

        await incidentRepository.UpdateAsync(incident, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class RecordIncidentPhoneCallCommandHandler(IAggregateRootRepository<BoardingIncidentAggregate> incidentRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordIncidentPhoneCallCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordIncidentPhoneCallCommand command)
    {
        var incident = await incidentRepository.GetByIdAsync(command.IncidentId);
        if (incident is null || incident.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.IncidentNotFound, nameof(command.IncidentId), "Incident not found.", nameof(BoardingIncidentAggregate));

        try
        {
            incident.RecordPhoneCall(timeProvider.GetUtcNow(), command.Outcome, command.OwnerReached, command.UserContext!);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(BoardingIncidentAggregate));
        }

        await incidentRepository.UpdateAsync(incident, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class ResolveIncidentCommandHandler(IAggregateRootRepository<BoardingIncidentAggregate> incidentRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<ResolveIncidentCommand>
{
    public async Task<CommandResponse> HandleAsync(ResolveIncidentCommand command)
    {
        var incident = await incidentRepository.GetByIdAsync(command.IncidentId);
        if (incident is null || incident.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.IncidentNotFound, nameof(command.IncidentId), "Incident not found.", nameof(BoardingIncidentAggregate));

        try
        {
            incident.Resolve(timeProvider.GetUtcNow(), command.Resolution, command.UserContext!);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(BoardingIncidentAggregate));
        }

        await incidentRepository.UpdateAsync(incident, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
