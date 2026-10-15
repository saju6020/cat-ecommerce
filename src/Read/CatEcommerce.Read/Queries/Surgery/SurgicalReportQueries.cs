using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Surgery;

/// <summary>US-SRG-05 -- the report of a surgery of the caller active branch, or <c>null</c> (no report yet, or another branch). A clinical-record access, logged by the controller.</summary>
public sealed class GetSurgicalReportQuery : Query<SurgicalReportViewModel?>
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
}
