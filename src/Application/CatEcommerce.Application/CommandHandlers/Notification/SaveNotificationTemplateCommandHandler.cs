using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Notification;

/// <summary>HO-NTF-001 §11.4 C1 / US-NTF-02.</summary>
public sealed class SaveNotificationTemplateCommandHandler(
    ILogger<SaveNotificationTemplateCommandHandler> logger,
    INotificationTemplateCatalogue catalogue,
    IAggregateRootRepository<NotificationTemplateAggregate> templateRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SaveNotificationTemplateCommand>
{
    public async Task<CommandResponse> HandleAsync(SaveNotificationTemplateCommand command)
    {
        logger.LogInformation("Saving notification template {Key}. CorrelationId: {CorrelationId}", command.Key, command.CorrelationId);

        var definition = catalogue.Find(command.Key);
        if (definition is null)
            return Violation(BusinessViolationCodes.TemplateNotInCatalogue, nameof(command.Key), $"'{command.Key}' is not a known template.");

        // Defense in depth alongside SaveNotificationTemplateCommandValidator's own shape rules
        // (gate fix 2026-09-27, SECURITY LOW): never let a null entry or an unrecognised channel
        // name reach Enumeration.FromDisplayName unguarded.
        var contents = new List<TemplateContentInput>();
        foreach (var item in command.Contents)
        {
            if (item is null)
                return Violation(BusinessViolationCodes.InvalidTemplateContents, nameof(command.Contents), "Contents must not contain a null entry.");

            if (!EnumerationParsing.TryParse<NotificationChannel>(item.Channel, out var parsedChannel) || parsedChannel is null)
                return Violation(BusinessViolationCodes.InvalidTemplateChannel, nameof(command.Contents), $"'{item.Channel}' is not a known channel.");

            contents.Add(new TemplateContentInput(parsedChannel, item.Subject, item.Title, item.Body, item.LinkPath));
        }

        var template = await templateRepository.GetByFilterAsync(t => t.Key == command.Key);
        var currentVersion = template?.ContentVersion ?? 0;

        if (command.ExpectedContentVersion != currentVersion)
            return Violation(BusinessViolationCodes.TemplateVersionConflict, nameof(command.ExpectedContentVersion),
                $"Expected content version {command.ExpectedContentVersion} but the template is at {currentVersion}.");

        var dto = new NotificationTemplateCustomiseDto(definition, contents, command.UserContext.UserId, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId);

        try
        {
            if (template is null)
            {
                template = new NotificationTemplateAggregate(Guid.NewGuid());
                template.Customise(dto);
                await templateRepository.SaveAsync(template, EventPublishOption.InMemoryPublishOnly);
            }
            else
            {
                template.Edit(dto);
                await templateRepository.UpdateAsync(template, EventPublishOption.InMemoryPublishOnly);
            }
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(NotificationTemplateAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
