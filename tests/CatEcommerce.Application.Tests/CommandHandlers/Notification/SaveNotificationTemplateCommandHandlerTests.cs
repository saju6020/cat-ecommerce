using CatEcommerce.Application.CommandHandlers.Notification;
using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.CommandHandlers.Notification;

/// <summary>HO-NTF-001 §15/§16 C1 -- I-A2/I-A3/I-A5 exercised as unit tests here.</summary>
public sealed class SaveNotificationTemplateCommandHandlerTests
{
    private static SaveNotificationTemplateCommand ValidCommand(int expectedVersion) => new()
    {
        Key = NotificationTemplateCatalogue.VaccinationDue,
        ExpectedContentVersion = expectedVersion,
        Contents =
        [
            new NotificationTemplateContentItem { Channel = "Sms", Body = "{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}" },
            new NotificationTemplateContentItem { Channel = "Email", Subject = "Reminder", Body = "{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}" },
            new NotificationTemplateContentItem { Channel = "InApp", Title = "Reminder", Body = "{{catName}} {{vaccineName}} {{dueOn}}", LinkPath = "{{bookingLink}}" },
        ],
        UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
    };

    [Fact]
    public async Task UnknownKey_ReturnsTemplateNotInCatalogue()
    {
        var repository = new FakeAggregateRootRepository<NotificationTemplateAggregate>();
        var handler = new SaveNotificationTemplateCommandHandler(NullLogger<SaveNotificationTemplateCommandHandler>.Instance, new FakeNotificationTemplateCatalogue(), repository, new FakeTimeProvider());

        var command = new SaveNotificationTemplateCommand
        {
            Key = "does.not-exist", ExpectedContentVersion = 0, Contents = ValidCommand(0).Contents,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };
        var response = await handler.HandleAsync(command);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.TemplateNotInCatalogue);
    }

    [Fact]
    public async Task FirstSave_CreatesCustomisedTemplateAtVersion1()
    {
        var repository = new FakeAggregateRootRepository<NotificationTemplateAggregate>();
        var handler = new SaveNotificationTemplateCommandHandler(NullLogger<SaveNotificationTemplateCommandHandler>.Instance, new FakeNotificationTemplateCatalogue(), repository, new FakeTimeProvider());

        var response = await handler.HandleAsync(ValidCommand(0));

        Assert.True(response.IsSuccess);
        Assert.Single(repository.Saved);
        Assert.Equal(1, repository.Saved[0].ContentVersion);
    }

    [Fact]
    public async Task InvalidPlaceholder_ReturnsUnknownTemplatePlaceholder()
    {
        var repository = new FakeAggregateRootRepository<NotificationTemplateAggregate>();
        var handler = new SaveNotificationTemplateCommandHandler(NullLogger<SaveNotificationTemplateCommandHandler>.Instance, new FakeNotificationTemplateCatalogue(), repository, new FakeTimeProvider());

        var command = ValidCommand(0);
        var badContents = command.Contents.Select(c => c.Channel == "Sms"
            ? new NotificationTemplateContentItem { Channel = "Sms", Body = "{{catname}} due" }
            : c).ToList();
        var badCommand = new SaveNotificationTemplateCommand { Key = command.Key, ExpectedContentVersion = 0, Contents = badContents, UserContext = command.UserContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true };

        var response = await handler.HandleAsync(badCommand);

        Assert.False(response.IsSuccess);
        var error = Assert.Single(response.ValidationResult!.Errors);
        Assert.Equal(BusinessViolationCodes.UnknownTemplatePlaceholder, error.ErrorCode);
        Assert.Contains("{{catname}}", error.ErrorMessage);
    }

    [Fact]
    public async Task WrongExpectedVersion_ReturnsTemplateVersionConflict()
    {
        var repository = new FakeAggregateRootRepository<NotificationTemplateAggregate>();
        var handler = new SaveNotificationTemplateCommandHandler(NullLogger<SaveNotificationTemplateCommandHandler>.Instance, new FakeNotificationTemplateCatalogue(), repository, new FakeTimeProvider());

        var first = await handler.HandleAsync(ValidCommand(0));
        Assert.True(first.IsSuccess);

        var second = await handler.HandleAsync(ValidCommand(0)); // should be 1 now
        Assert.False(second.IsSuccess);
        Assert.Contains(second.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.TemplateVersionConflict);
    }
}
