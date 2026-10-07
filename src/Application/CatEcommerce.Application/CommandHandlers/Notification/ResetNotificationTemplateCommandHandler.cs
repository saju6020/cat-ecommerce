using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Notification;

/// <summary>HO-NTF-001 §11.4 C2.</summary>
public sealed class ResetNotificationTemplateCommandHandler(
    ILogger<ResetNotificationTemplateCommandHandler> logger,
    IAggregateRootRepository<NotificationTemplateAggregate> templateRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ResetNotificationTemplateCommand>
{
    public async Task<CommandResponse> HandleAsync(ResetNotificationTemplateCommand command)
    {
        logger.LogInformation("Resetting notification template {Key}. CorrelationId: {CorrelationId}", command.Key, command.CorrelationId);

        var template = await templateRepository.GetByFilterAsync(t => t.Key == command.Key);
        if (template is null) return new CommandResponse { IsSuccess = true };

        if (command.ExpectedContentVersion != template.ContentVersion)
        {
            var validationResponse = new ValidationResponse();
            validationResponse.AddError(
                errorMessage: $"Expected content version {command.ExpectedContentVersion} but the template is at {template.ContentVersion}.",
                propertyName: nameof(command.ExpectedContentVersion), errorCode: BusinessViolationCodes.TemplateVersionConflict, resourceName: nameof(NotificationTemplateAggregate));
            return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
        }

        template.ResetToDefault(command.UserContext.UserId, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId);
        await templateRepository.UpdateAsync(template, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }
}
