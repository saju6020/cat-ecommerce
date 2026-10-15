using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Inventory;

/// <summary>The recall register. Recalls are not scoped to a branch: a recall applies to the batch everywhere.</summary>
public sealed class GetRecallsQuery : Query<PagedResult<RecallSummaryViewModel>>
{
    public string? Status { get; init; }
}

public sealed class GetRecallQuery : Query<RecallDetailViewModel?>
{
    public required Guid BatchId { get; init; }
}

/// <summary>US-IMP-11 AC2 -- who received the batch, who got it back, what is reserved but unshipped, how many sales are unidentified and what is still held.</summary>
public sealed class GetRecallTraceQuery : Query<RecallTraceViewModel?>
{
    public required Guid BatchId { get; init; }
}
