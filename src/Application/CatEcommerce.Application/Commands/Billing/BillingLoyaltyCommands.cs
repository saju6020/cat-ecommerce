using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Billing;

/// <summary>HO-BIL-001 section 11.1 C27 (US-BIL-10 AC2) -- redeem points on a <b>draft</b> invoice as a discount.</summary>
public sealed class ApplyLoyaltyRedemptionCommand : Command
{
    public required Guid InvoiceId { get; init; }
    public required Guid BranchId { get; init; }
    public required int Points { get; init; }
}

/// <summary>C27 -- take the redemption off a draft; the points go back to the customer.</summary>
public sealed class RemoveLoyaltyRedemptionCommand : Command
{
    public required Guid InvoiceId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>HO-BIL-001 section 11.1 C13 (US-BIL-08) -- pay an invoice from the customer free credit (payments that are not earmarked, or whose earmark was
/// released), oldest first. <see cref="Amount"/> null means as much as possible.</summary>
public sealed class ApplyCustomerCreditCommand : Command
{
    public required Guid CustomerId { get; init; }
    public required Guid InvoiceId { get; init; }
    public required Guid BranchId { get; init; }
    public decimal? Amount { get; init; }
}
