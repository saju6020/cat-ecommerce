using CatEcommerce.Application.CommandHandlers.Notification;
using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.CommandHandlers.Notification;

/// <summary>HO-NTF-001 §15/§16 C8 -- I-D8/I-D9. Another customer's item is 404-shaped
/// (NotificationNotFound), never a distinguishable "forbidden".</summary>
public sealed class MarkInboxItemReadCommandHandlerTests
{
    private static NotificationAggregate InboxItem(Guid recipientId, DateTimeOffset now)
    {
        var notification = new NotificationAggregate(Guid.NewGuid());
        notification.CreateInbox(new NotificationCreateDto(
            Guid.NewGuid(), "care.vaccination-due", 0, "en", NotificationCategory.Transactional, NotificationUrgency.Normal, false,
            NotificationRecipientKind.Customer, recipientId, null, null, null, null, null, NotificationChannel.InApp, null,
            null, "Title", "Body", null, null, null, now, null, false, null, null, null, null, null));
        return notification;
    }

    [Fact]
    public async Task OwnItem_MarksRead()
    {
        var repository = new FakeAggregateRootRepository<NotificationAggregate>();
        var customerId = Guid.NewGuid();
        var item = InboxItem(customerId, DateTimeOffset.UtcNow);
        repository.Seed(item);

        var handler = new MarkInboxItemReadCommandHandler(repository, new FakeTimeProvider());
        var response = await handler.HandleAsync(new MarkInboxItemReadCommand
        {
            NotificationId = item.Id, UserContext = new Platform.Infrastructure.Common.Security.UserContext(customerId, Guid.NewGuid()), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.NotNull(item.ReadOn);
    }

    [Fact]
    public async Task AnotherCustomersItem_ReturnsNotificationNotFound()
    {
        var repository = new FakeAggregateRootRepository<NotificationAggregate>();
        var item = InboxItem(Guid.NewGuid(), DateTimeOffset.UtcNow);
        repository.Seed(item);

        var handler = new MarkInboxItemReadCommandHandler(repository, new FakeTimeProvider());
        var response = await handler.HandleAsync(new MarkInboxItemReadCommand
        {
            NotificationId = item.Id, UserContext = new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.NotificationNotFound);
        Assert.Null(item.ReadOn);
    }
}
