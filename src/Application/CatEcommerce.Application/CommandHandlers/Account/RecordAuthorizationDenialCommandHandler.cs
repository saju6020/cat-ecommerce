using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>HO-ACC-001 §11.4 (C11) / §12.8. A denial mutates no aggregate, so there is no event
/// to hang the audit on -- written directly here.</summary>
public sealed class RecordAuthorizationDenialCommandHandler(
    ILogger<RecordAuthorizationDenialCommandHandler> logger,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordAuthorizationDenialCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordAuthorizationDenialCommand command)
    {
        logger.LogInformation("Recording authorization denial for action {Action}. CorrelationId: {CorrelationId}", command.Action, command.CorrelationId);

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        var roles = string.Join(",", command.UserContext.Roles ?? []);

        entry.Record(new AuditEntryDto(
            EntityType: "Authorization",
            EntityId: command.UserContext.UserId,
            Action: "AuthorizationDenied",
            Reason: command.Action,
            ActorUserId: command.UserContext.UserId,
            OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null,
            AfterValue: $"Method={command.Method};Path={command.Path};Roles={roles}",
            UserContext: command.UserContext,
            CorrelationId: command.CorrelationId,
            Category: AuditCategory.AuthenticationEvent,
            IpAddress: command.IpAddress is null ? null : System.Net.IPAddress.Parse(command.IpAddress)));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);

        return new CommandResponse { IsSuccess = true };
    }
}
