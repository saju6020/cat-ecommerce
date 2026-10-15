using CatEcommerce.Infrastructure.Billing.Export;
using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Billing;

/// <summary>HO-BIL-001 section 12.6 (ADR-190) -- the immutable facts dated in a range, for the accounting export. Never a current status.
/// <see cref="BranchId"/> null means every branch.</summary>
public sealed class GetAccountingExportContentQuery : Query<AccountingExportContent>
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public Guid? BranchId { get; init; }
}

/// <summary>Who exported what, when, with the fingerprint of the file.</summary>
public sealed class GetAccountingExportRunsQuery : Query<PagedResult<AccountingExportRunViewModel>>
{
}
