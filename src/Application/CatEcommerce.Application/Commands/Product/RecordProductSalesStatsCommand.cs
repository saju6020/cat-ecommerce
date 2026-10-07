using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

/// <summary>
/// HO-SHP-001 section 8.7 / section 11.1 C10 -- **inbound, in-process only**. Dispatched by
/// HO-ORD-001 (and tests) through <c>IInMemoryDispatcher</c>; never reachable over HTTP -- no
/// controller in this slice sends it (section 12 security note: "the inbound commands unreachable
/// over HTTP").
/// </summary>
public sealed class RecordProductSalesStatsCommand : Command
{
    public required Guid ProductId { get; init; }
    public required int UnitsSold90d { get; init; }
    public required DateTimeOffset AsOf { get; init; }
}
