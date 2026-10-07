using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Administration;

/// <summary>HO-ADM-001 §12.6 -- <c>OccurredOn</c> range is required and capped at
/// <see cref="CatEcommerce.Shared.Common.Constants.AuditExportRules.MaxDateRangeDays"/> (validated
/// by the controller, U-E2).</summary>
public sealed class GetAuditEntriesQuery : Query<PagedResult<AuditEntryViewModel>>
{
    public Guid? ActorUserId { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? Action { get; init; }
    public string? Category { get; init; }
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
}
