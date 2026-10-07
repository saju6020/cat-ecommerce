using System.Text.Json;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Administration;

/// <summary>HO-ADM-001 §11.4 (C11) -- writes one <see cref="AuditEntryAggregate"/> directly, like
/// HO-ACC-001's C11 (reading the audit trail is itself a sensitive act, ADR-051).</summary>
public sealed class RecordAuditLogExportCommandHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordAuditLogExportCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordAuditLogExportCommand command)
    {
        var filters = JsonSerializer.Serialize(new
        {
            command.ActorUserId, command.EntityType, command.EntityId, command.Action, command.Category,
            command.From, command.To, command.RowCount
        });

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "AuditLog", EntityId: command.UserContext.UserId, Action: "AuditLogExported", Reason: "AuditLogExported",
            ActorUserId: command.UserContext.UserId, OccurredOn: timeProvider.GetUtcNow(), BeforeValue: null, AfterValue: filters,
            UserContext: command.UserContext, CorrelationId: command.CorrelationId, Category: AuditCategory.BusinessEvent,
            IpAddress: command.IpAddress is null ? null : System.Net.IPAddress.Parse(command.IpAddress)));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
        return new CommandResponse { IsSuccess = true };
    }
}
