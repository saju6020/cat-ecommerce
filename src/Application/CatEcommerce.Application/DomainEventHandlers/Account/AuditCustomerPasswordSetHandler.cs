using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Account;

/// <summary>HO-ACC-001 §11.6/§11.7.</summary>
public sealed class AuditCustomerPasswordSetHandler(
    ILogger<AuditCustomerPasswordSetHandler> logger,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<CustomerPasswordSetEvent>
{
    public async Task HandleAsync(CustomerPasswordSetEvent @event)
    {
        logger.LogInformation("Auditing customer password set {CustomerId}. CorrelationId: {CorrelationId}", @event.CustomerId, @event.CorrelationId);

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "CustomerAccount", EntityId: @event.CustomerId, Action: "CustomerPasswordSet",
            Reason: @event.WasFirstPassword ? "FirstPassword" : "PasswordReset", ActorUserId: @event.CustomerId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null, AfterValue: null, UserContext: @event.UserContext, CorrelationId: @event.CorrelationId,
            Category: AuditCategory.AuthenticationEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
