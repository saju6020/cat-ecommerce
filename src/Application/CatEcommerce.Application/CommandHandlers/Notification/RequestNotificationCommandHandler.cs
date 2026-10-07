using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Notification;

/// <summary>
/// HO-NTF-001 §11.4 C3 -- sent by timed producers (with a deduplication key) and by tests. A
/// business command handler enqueues through <see cref="INotificationOutbox"/> directly (§8.4);
/// this command exists for producers that have no business change of their own to ride on.
/// </summary>
public sealed class RequestNotificationCommandHandler(
    ILogger<RequestNotificationCommandHandler> logger,
    INotificationTemplateCatalogue catalogue,
    IAggregateRootRepository<NotificationRequestAggregate> requestRepository,
    INotificationRequestStore requestStore,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RequestNotificationCommand>
{
    public async Task<CommandResponse> HandleAsync(RequestNotificationCommand command)
    {
        logger.LogInformation("Notification requested for template {TemplateKey}. CorrelationId: {CorrelationId}", command.TemplateKey, command.CorrelationId);

        var definition = catalogue.Find(command.TemplateKey);
        if (definition is null)
            return Violation(BusinessViolationCodes.TemplateNotInCatalogue, nameof(command.TemplateKey), $"'{command.TemplateKey}' is not a known template.");

        var knownVariableNames = definition.Variables.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);
        var unknownVariable = command.Variables.Keys.FirstOrDefault(k => !knownVariableNames.Contains(k));
        if (unknownVariable is not null)
            return Violation(BusinessViolationCodes.InvalidNotificationRequest, nameof(command.Variables), $"'{unknownVariable}' is not a variable of '{command.TemplateKey}'.");

        if (!string.IsNullOrEmpty(command.DeduplicationKey))
        {
            var alreadyRequested = await requestRepository.ExistAsync(r => r.DeduplicationKey == command.DeduplicationKey);
            if (alreadyRequested) return new CommandResponse { IsSuccess = true };
        }

        var dto = command.ToDomainDto();
        var aggregate = new NotificationRequestAggregate(dto.RequestId);
        aggregate.Create(new NotificationRequestCreateDto(
            dto.TemplateKey, dto.RecipientKind, dto.RecipientId, dto.Variables, dto.ChannelHint,
            dto.SubjectKind, dto.SubjectId, dto.SubjectRef, dto.SubjectDueOn, dto.BranchId,
            dto.DeduplicationKey, dto.NotAfter, definition.ContainsSecret,
            timeProvider.GetUtcNow(), command.UserContext.UserId, command.UserContext, command.CorrelationId));

        var saved = await requestStore.TrySaveAsync(aggregate);
        // !saved: the race was lost cleanly -- another request with the same key won. Success either way.
        var response = new CommandResponse { IsSuccess = true };
        if (saved) response.Result = aggregate.Id;
        return response;
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(NotificationRequestAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
