using System.Net;
using CatEcommerce.Application.Commands.StoredFile;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.StoredFile;

/// <summary>HO-FIL-001 §8.2 step 5. Never logs file content or the original file name -- only the
/// violation code, purpose and scanner signature (§8.2's "no content").</summary>
public sealed class RecordFileRejectionAuditCommandHandler(
    ILogger<RecordFileRejectionAuditCommandHandler> logger,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordFileRejectionAuditCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordFileRejectionAuditCommand command)
    {
        logger.LogWarning(
            "File upload rejected: {ViolationCode} for purpose {Purpose}. CorrelationId: {CorrelationId}",
            command.ViolationCode, command.Purpose, command.CorrelationId);

        var entry = new AuditEntryAggregate(Guid.NewGuid());

        entry.Record(new AuditEntryDto(
            EntityType: "StoredFile",
            EntityId: command.AttemptedFileId,
            Action: "FileUploadRejected",
            Reason: command.ViolationCode,
            ActorUserId: command.UserContext.UserId,
            OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null,
            AfterValue: $"Purpose={command.Purpose};Scanner={command.ScannerName ?? "n/a"}",
            UserContext: command.UserContext,
            CorrelationId: command.CorrelationId,
            Category: AuditCategory.BusinessEvent,
            IpAddress: command.IpAddress is null ? null : IPAddress.Parse(command.IpAddress)));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);

        return new CommandResponse { IsSuccess = true };
    }
}
