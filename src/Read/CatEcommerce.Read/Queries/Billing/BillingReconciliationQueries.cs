using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Billing;

/// <summary>HO-BIL-001 section 12.4 (US-BIL-06 AC1) -- courier-collected COD payments with what was remitted and what is still owed.</summary>
public sealed class GetCodCollectionsQuery : Query<PagedResult<CodCollectionViewModel>>
{
    public required Guid BranchId { get; init; }
    public string? CourierCode { get; init; }

    /// <summary><c>unremitted</c>, <c>shortfall</c> or <c>remitted</c>.</summary>
    public string? Status { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class GetCodRemittancesQuery : Query<PagedResult<CodRemittanceViewModel>>
{
    public required Guid BranchId { get; init; }
    public string? CourierCode { get; init; }
}

public sealed class GetCodReconciliationQuery : Query<CodReconciliationViewModel>
{
    public required Guid BranchId { get; init; }
    public string? CourierCode { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class GetBankStatementsQuery : Query<PagedResult<BankStatementSummaryViewModel>>
{
    public required Guid BranchId { get; init; }
}

public sealed class GetBankStatementReconciliationQuery : Query<BankStatementReconciliationViewModel?>
{
    public required Guid BranchId { get; init; }
    public required Guid StatementId { get; init; }
}
