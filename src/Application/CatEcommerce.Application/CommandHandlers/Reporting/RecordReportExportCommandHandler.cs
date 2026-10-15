using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CatEcommerce.Application.Commands.Reporting;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Reporting;

/// <summary>
/// HO-RPT-001 section 11.4 (C1, ADR-285) -- writes one <see cref="AuditEntryAggregate"/> (category <c>BusinessEvent</c>, action <c>ReportExported</c>, entity type <c>Report</c>) through HO-ACC-001's
/// audit factory. One aggregate. The entity id is a deterministic GUID from the report key, so every export of one report shares an entity id and can be listed together. The payload holds the
/// filters, the row count and the visible column keys -- never a row value (HO-ADM-001 ADR-051's precedent).
/// </summary>
public sealed class RecordReportExportCommandHandler(IAggregateRootRepository<AuditEntryAggregate> auditRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordReportExportCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordReportExportCommand command)
    {
        var afterValue = JsonSerializer.Serialize(new
        {
            reportKey = command.ReportKey,
            format = command.Format.DisplayName,
            from = command.From.ToString("yyyy-MM-dd"),
            to = command.To.ToString("yyyy-MM-dd"),
            branchId = command.BranchId ?? "active",
            parameters = command.Parameters,
            rowCount = command.RowCount,
            columnKeys = command.ColumnKeys,
        });

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: ReportRules.ExportAuditEntityType, EntityId: EntityIdFor(command.ReportKey), Action: ReportRules.ExportAuditAction, Reason: ReportRules.ExportAuditAction,
            ActorUserId: command.UserContext.UserId, OccurredOn: timeProvider.GetUtcNow(), BeforeValue: null, AfterValue: afterValue,
            UserContext: command.UserContext, CorrelationId: command.CorrelationId, Category: AuditCategory.BusinessEvent,
            IpAddress: IPAddress.TryParse(command.IpAddress, out var address) ? address : null));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
        return new CommandResponse { IsSuccess = true };
    }

    /// <summary>A stable GUID per report key (the first 16 bytes of its SHA-256).</summary>
    public static Guid EntityIdFor(string reportKey) => new(SHA256.HashData(Encoding.UTF8.GetBytes("report:" + reportKey))[..16]);
}
