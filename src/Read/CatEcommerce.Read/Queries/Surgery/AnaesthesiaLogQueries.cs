using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Surgery;

/// <summary>US-SRG-04 AC2 -- every entry ordered by when it happened, then when it was logged; drugs, vitals, milestones and corrections interleaved on one axis. A clinical-record access
/// (logged by the controller). Another branch surgery is "not found" (null).</summary>
public sealed class GetAnaesthesiaLogQuery : Query<AnaesthesiaLogViewModel?>
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
}
