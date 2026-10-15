using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Billing;

/// <summary>HO-BIL-001 section 12.4 (US-BIL-10) -- a customer points balance and ledger (staff for any customer, a customer for themselves).</summary>
public sealed class GetLoyaltyQuery : Query<LoyaltyViewModel>
{
    public required Guid CustomerId { get; init; }
}
