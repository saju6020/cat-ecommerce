using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Administration;

/// <summary>HO-ADM-001 §11.1 (C11) -- the filters used for a search-export, recorded as the
/// audit-of-the-audit itself (ADR-051).</summary>
public sealed class RecordAuditLogExportCommand : Command
{
    public Guid? ActorUserId { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? Action { get; init; }
    public string? Category { get; init; }
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public required int RowCount { get; init; }
    public string? IpAddress { get; init; }
}
