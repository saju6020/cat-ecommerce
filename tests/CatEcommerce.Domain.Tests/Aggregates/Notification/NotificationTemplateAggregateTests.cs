using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.Aggregates.Notification;

/// <summary>HO-NTF-001 §15 U-A13.</summary>
public sealed class NotificationTemplateAggregateTests
{
    private static readonly NotificationTemplateDefinition Definition = NotificationTemplateCatalogue.Find(NotificationTemplateCatalogue.VaccinationDue)!;

    private static IReadOnlyList<TemplateContentInput> ValidContents(string suffix) =>
    [
        new TemplateContentInput(NotificationChannel.Sms, null, null, $"{{{{catName}}}} {{{{vaccineName}}}} {{{{dueOn}}}} {{{{bookingLink}}}} {suffix}", null),
        new TemplateContentInput(NotificationChannel.Email, "Subject", null, $"{{{{catName}}}} {{{{vaccineName}}}} {{{{dueOn}}}} {{{{bookingLink}}}} {suffix}", null),
        new TemplateContentInput(NotificationChannel.InApp, null, "Title", $"{{{{catName}}}} {{{{vaccineName}}}} {{{{dueOn}}}} {suffix}", "{{bookingLink}}"),
    ];

    [Fact]
    public void EditTwiceThenReset_VersionsIncrementThenContentsCleared()
    {
        var template = new NotificationTemplateAggregate(Guid.NewGuid());
        var actorId = Guid.NewGuid();

        template.Customise(new NotificationTemplateCustomiseDto(Definition, ValidContents("v1"), actorId, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(1, template.ContentVersion);

        template.Edit(new NotificationTemplateCustomiseDto(Definition, ValidContents("v2"), actorId, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(2, template.ContentVersion);
        Assert.Contains(template.Contents, c => c.Body.EndsWith("v2"));

        template.ResetToDefault(actorId, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(3, template.ContentVersion);
        Assert.Empty(template.Contents);
    }
}
