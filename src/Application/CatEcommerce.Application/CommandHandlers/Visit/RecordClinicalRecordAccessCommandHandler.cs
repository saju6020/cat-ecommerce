using System.Net;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Visit;

/// <summary>
/// HO-EMR-001 C4 / §8.11 / ADR-129 -- writes one <c>ClinicalRecordAccess</c> audit row per read of a
/// clinical record (NFR-SEC-8). The row's <c>EntityType</c> is the <see cref="ClinicalRecordKind"/>
/// name (registered in <c>ClinicalAuditEntityTypes.Consultation</c>), <c>EntityId</c> the record or
/// the cat. Nothing clinical is stored: the path is the route only (ids and section names), never
/// the query string, and the row is never published (<c>DoNotPublish</c>).
/// A failure here propagates: the filter turns it into a 500 and the action never runs.
/// </summary>
public sealed class RecordClinicalRecordAccessCommandHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordClinicalRecordAccessCommand>
{
    public const string ActionName = "ClinicalRecordRead";

    public async Task<CommandResponse> HandleAsync(RecordClinicalRecordAccessCommand command)
    {
        var entry = new AuditEntryAggregate(command.AuditEntryId);

        entry.Record(new AuditEntryDto(
            EntityType: command.Kind.DisplayName,
            EntityId: command.RecordId ?? command.CatId ?? Guid.Empty,
            Action: ActionName,
            Reason: ActionName,
            ActorUserId: command.UserContext.UserId,
            OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null,
            AfterValue: command.CatId is { } catId ? $"Path={command.Path};CatId={catId}" : $"Path={command.Path}",
            UserContext: command.UserContext,
            CorrelationId: command.CorrelationId,
            Category: AuditCategory.ClinicalRecordAccess,
            IpAddress: command.IpAddress is null ? null : IPAddress.Parse(command.IpAddress)));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
        return new CommandResponse { IsSuccess = true };
    }
}
