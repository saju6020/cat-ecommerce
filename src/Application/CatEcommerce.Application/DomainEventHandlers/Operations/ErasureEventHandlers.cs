using System.Text.Json;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Operations;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 11.6 / ADR-302. When an account is anonymised, open its erasure ledger (command C1) in a NEW scope, so a ledger failure can
/// never poison the anonymisation own unit of work. The source is <c>RestoreReconciliation</c> when the correlation carries the C4 marker,
/// <c>SelfService</c> when the caller is the customer, otherwise <c>StaffOnBehalf</c>.
///
/// A failure here fails the deletion request after the anonymisation committed (ADR-020; <c>.ai/domain-events-and-outbox.md</c> section 3): it is
/// logged at Error with the customer id and rethrown, and the retention worker erasure pass SELF-HEALS by opening a ledger for any Anonymised
/// account that has none (section 12.2), so the request is never lost.
/// </summary>
public sealed class OpenErasureRequestOnCustomerAnonymisedHandler(
    IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<OpenErasureRequestOnCustomerAnonymisedHandler> logger)
    : IEventHandlerAsync<CustomerAccountAnonymisedEvent>
{
    public static ErasureRequestSource SourceFor(Guid correlationId, UserContext userContext)
    {
        if (RetentionCorrelation.IsReapply(correlationId)) return ErasureRequestSource.RestoreReconciliation;

        var roles = userContext.Roles ?? [];
        return roles.Length == 1 && roles[0] == "Customer" ? ErasureRequestSource.SelfService : ErasureRequestSource.StaffOnBehalf;
    }

    public async Task HandleAsync(CustomerAccountAnonymisedEvent @event)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();

            var response = await dispatcher.SendAsync(new OpenErasureRequestCommand
            {
                CustomerAccountId = @event.CustomerId,
                RequestedOn = timeProvider.GetUtcNow(),
                Source = SourceFor(@event.CorrelationId, @event.UserContext).DisplayName,
                UserContext = @event.UserContext,
                CorrelationId = @event.CorrelationId,
                ValidateCommand = true,
            });

            if (!response.IsSuccess)
                throw new InvalidOperationException("The erasure ledger could not be opened: " + string.Join(",", response.ValidationResult?.Errors.Select(e => e.ErrorCode) ?? []));
        }
        catch (Exception ex)
        {
            logger.LogError("Opening the erasure ledger for anonymised customer {CustomerId} failed ({ExceptionType}); the retention worker will open it. CorrelationId: {CorrelationId}",
                @event.CustomerId, ex.GetType().Name, @event.CorrelationId);
            throw;
        }
    }
}

/// <summary>HO-XCT-001 section 11.6. One business-event row when a ledger completes: customer id and completed-on. This is the proof point for D-11.</summary>
public sealed class AuditErasureRequestCompletedHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository, IClientIpAccessor clientIpAccessor, TimeProvider timeProvider)
    : IEventHandlerAsync<ErasureRequestCompletedEvent>
{
    public async Task HandleAsync(ErasureRequestCompletedEvent @event)
    {
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "ErasureRequest", EntityId: @event.CustomerAccountId, Action: "ErasureRequestCompleted",
            Reason: "All contexts erased or released", ActorUserId: @event.UserContext.UserId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null, AfterValue: JsonSerializer.Serialize(new { completedOn = @event.CompletedOn }),
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId, Category: AuditCategory.BusinessEvent,
            IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
