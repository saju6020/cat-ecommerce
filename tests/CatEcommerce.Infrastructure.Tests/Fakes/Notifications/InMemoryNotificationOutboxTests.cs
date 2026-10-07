using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Fakes.Notifications;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Tests.Fakes.Notifications;

public sealed class InMemoryNotificationOutboxTests
{
    [Fact]
    public void Enqueue_records_the_request()
    {
        var sut = new InMemoryNotificationOutbox();
        var request = new NotificationRequestDto(
            Guid.NewGuid(), "account.password-reset", NotificationRecipientKind.Customer, Guid.NewGuid(),
            new Dictionary<string, string>());

        sut.Enqueue(request);

        Assert.Single(sut.Requests);
        Assert.Same(request, sut.Requests.Single());
    }
}
