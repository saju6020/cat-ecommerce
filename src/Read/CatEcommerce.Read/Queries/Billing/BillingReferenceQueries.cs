using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Billing;

/// <summary>HO-BIL-001 §12.4 -- the payment-method flags, so the UI renders fields from data (ADR-182).</summary>
public sealed class GetPaymentMethodsQuery : Query<IReadOnlyList<PaymentMethodViewModel>>
{
}

/// <summary>HO-BIL-001 §12.4 -- null when the branch has no profile yet.</summary>
public sealed class GetBillingProfileQuery : Query<BillingProfileViewModel?>
{
    public required Guid BranchId { get; init; }
}
