using CatEcommerce.Application.CommandHandlers.Notification;
using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Notification;

/// <summary>
/// SECURITY/CODE_REVIEW gate fix (2026-09-27, second pass, MEDIUM, "one bad row blocks the whole
/// queue"). Dispatched by NotificationWorkCycle, from a fresh scope, when
/// ExpandNotificationRequestCommand's own dispatch throws.
/// </summary>
public sealed class RecordNotificationExpansionFailureCommandHandlerTests
{
    private static NotificationRequestAggregate PendingRequest()
    {
        var request = new NotificationRequestAggregate(Guid.NewGuid());
        request.Create(new NotificationRequestCreateDto(
            "care.vaccination-due", NotificationRecipientKind.Customer, Guid.NewGuid(),
            new Dictionary<string, string> { ["catName"] = "Mitu" },
            null, null, null, null, null, null, null, null, false,
            DateTimeOffset.UtcNow, Guid.NewGuid(), TestUserContext.Create(), Guid.NewGuid()));
        return request;
    }

    [Fact]
    public async Task PendingRequest_BelowThreshold_IncrementsAttemptCount_StaysPending()
    {
        var repository = new FakeAggregateRootRepository<NotificationRequestAggregate>();
        var request = PendingRequest();
        repository.Seed(request);
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = new RecordNotificationExpansionFailureCommandHandler(repository, time);

        var response = await handler.HandleAsync(new RecordNotificationExpansionFailureCommand
        {
            RequestId = request.Id, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        Assert.True(response.IsSuccess);
        var updated = Assert.Single(repository.Updated);
        Assert.Equal(NotificationRequestStatus.Pending, updated.Status);
        Assert.Equal(1, updated.AttemptCount);
    }

    [Fact]
    public async Task PendingRequest_AtThreshold_BecomesTerminal_PersistenceFailed()
    {
        var repository = new FakeAggregateRootRepository<NotificationRequestAggregate>();
        var request = PendingRequest();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        for (var i = 0; i < NotificationRequestRules.MaxExpansionAttempts - 1; i++)
            request.RecordFailedAttempt(time.GetUtcNow(), NotificationRequestRules.MaxExpansionAttempts);
        repository.Seed(request);
        var handler = new RecordNotificationExpansionFailureCommandHandler(repository, time);

        var response = await handler.HandleAsync(new RecordNotificationExpansionFailureCommand
        {
            RequestId = request.Id, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        Assert.True(response.IsSuccess);
        var updated = Assert.Single(repository.Updated);
        Assert.Equal(NotificationRequestStatus.Suppressed, updated.Status);
        Assert.Equal(NotificationSuppressionReason.PersistenceFailed, updated.SuppressionReason);
    }

    [Fact]
    public async Task RequestAlreadyExpanded_IsANoOp_NoUpdateWritten()
    {
        var repository = new FakeAggregateRootRepository<NotificationRequestAggregate>();
        var request = PendingRequest();
        request.MarkExpanded(DateTimeOffset.UtcNow);
        repository.Seed(request);
        var handler = new RecordNotificationExpansionFailureCommandHandler(repository, new FakeTimeProvider());

        var response = await handler.HandleAsync(new RecordNotificationExpansionFailureCommand
        {
            RequestId = request.Id, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        Assert.True(response.IsSuccess);
        Assert.Empty(repository.Updated);
    }

    [Fact]
    public async Task UnknownRequestId_IsANoOp_NeverThrows()
    {
        var repository = new FakeAggregateRootRepository<NotificationRequestAggregate>();
        var handler = new RecordNotificationExpansionFailureCommandHandler(repository, new FakeTimeProvider());

        var response = await handler.HandleAsync(new RecordNotificationExpansionFailureCommand
        {
            RequestId = Guid.NewGuid(), UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        Assert.True(response.IsSuccess);
        Assert.Empty(repository.Updated);
    }
}
