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

/// <summary>Owner, 2026-10-20: audits an administrator's edit of a staff member's name and sign-in identifiers, with
/// that administrator as the actor and the values before and after.</summary>
public sealed class AuditStaffAccountDetailsChangedHandler(
    ILogger<AuditStaffAccountDetailsChangedHandler> logger,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<StaffAccountDetailsChangedEvent>
{
    public async Task HandleAsync(StaffAccountDetailsChangedEvent @event)
    {
        logger.LogInformation("Auditing staff account details change {StaffAccountId}. CorrelationId: {CorrelationId}", @event.StaffAccountId, @event.CorrelationId);

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "StaffAccount", EntityId: @event.StaffAccountId, Action: "StaffAccountDetailsChanged",
            Reason: "ChangedByAdministrator", ActorUserId: @event.UserContext.UserId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: Describe(@event.PreviousFullName, @event.PreviousMobileNumber, @event.PreviousEmailAddress),
            AfterValue: Describe(@event.NewFullName, @event.NewMobileNumber, @event.NewEmailAddress),
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId,
            Category: AuditCategory.DataChange, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }

    private static string Describe(string fullName, string? mobile, string? email) =>
        $"FullName={fullName}; MobileNumber={mobile ?? "-"}; EmailAddress={email ?? "-"}";
}
