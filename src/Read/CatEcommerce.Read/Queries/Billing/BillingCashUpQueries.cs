using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Billing;

/// <summary>HO-BIL-001 section 12.4 (US-BIL-12 AC1) -- the day's expected totals, every row behind them, the count and the variance.</summary>
public sealed class GetCashUpQuery : Query<CashUpViewModel>
{
    public required Guid BranchId { get; init; }
    public required DateOnly BusinessDate { get; init; }
}
