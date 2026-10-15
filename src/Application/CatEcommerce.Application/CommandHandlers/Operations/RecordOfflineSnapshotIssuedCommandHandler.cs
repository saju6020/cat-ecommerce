using System.Text.Json;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Operations;

/// <summary>HO-XCT-001 section 11.4 C10 -- writes the <c>OfflineSnapshotIssued</c> business-event row. Nothing else: the snapshot itself is a read.</summary>
public sealed class RecordOfflineSnapshotIssuedCommandHandler(
    IAggregateRootRepository<AuditEntryAggregate> audit, IClientIpAccessor clientIp, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordOfflineSnapshotIssuedCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordOfflineSnapshotIssuedCommand command)
    {
        var after = JsonSerializer.Serialize(new
        {
            localDate = command.LocalDate.ToString("yyyy-MM-dd"),
            sections = command.SectionCounts,
            etag = command.ETag,
        });

        await OperationsAudit.WriteBusinessEventAsync(
            audit, clientIp, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId,
            entityType: "Branch", entityId: command.BranchId, action: "OfflineSnapshotIssued", reason: "Offline front-desk snapshot issued", afterValue: after);

        return OperationsResponses.Ok();
    }
}
