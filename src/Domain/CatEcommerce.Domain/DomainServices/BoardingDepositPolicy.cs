using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>The deposit a reservation requires and the state it starts in.</summary>
public sealed record DepositRequirement(decimal Amount, DepositState InitialState);

/// <summary>
/// HO-BRD-001 section 8.10 (US-BRD-06). Pure. A fixed deposit above zero wins, capped at the quote total; otherwise the percentage of the quote total
/// rounded to the paisa. A zero deposit is <see cref="DepositState.NotRequired"/>. The money is recorded by staff in Billing; nothing here captures any.
/// </summary>
public static class BoardingDepositPolicy
{
    public static DepositRequirement Compute(decimal quoteTotal, decimal fixedAmount, decimal percent)
    {
        var amount = fixedAmount > 0m
            ? Math.Min(fixedAmount, quoteTotal)
            : decimal.Round(quoteTotal * percent / 100m, 2, MidpointRounding.AwayFromZero);

        amount = Math.Max(0m, amount);
        return new DepositRequirement(amount, amount == 0m ? DepositState.NotRequired : DepositState.Awaiting);
    }
}
