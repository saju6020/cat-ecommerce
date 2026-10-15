using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 section 8.11 (US-BIL-10, ADR-191) -- a customer points balance. The id is the customer id (one account per customer, created on the first
/// entry). The balance never goes below zero: a reversal larger than the balance empties it and reports the <b>shortfall</b>, which the
/// ledger entry records. Every change here is paired with an append-only <see cref="LoyaltyEntryAggregate"/> in the same save; the account
/// is only the cached total of that ledger.
/// </summary>
public sealed class LoyaltyAccountAggregate : AggregateRoot
{
    public int Balance { get; private set; }

    private LoyaltyAccountAggregate() { }

    /// <summary>The id <b>is</b> the customer id.</summary>
    public LoyaltyAccountAggregate(Guid customerId) : base(customerId) { }

    public Guid CustomerId => Id;

    public void Open(UserContext userContext)
    {
        Balance = 0;
        SetDefaultValue(userContext);
        UpdateVersion();
    }

    public void Accrue(int points)
    {
        RequirePositive(points);
        Balance = checked(Balance + points);
        UpdateVersion();
    }

    public void Redeem(int points)
    {
        RequirePositive(points);
        if (points > Balance)
            throw new DomainRuleViolationException(BusinessViolationCodes.LoyaltyInsufficientPoints, $"The customer has {Balance} points; {points} were asked for.", nameof(points));

        Balance -= points;
        UpdateVersion();
    }

    public void ReverseRedemption(int points)
    {
        RequirePositive(points);
        Balance = checked(Balance + points);
        UpdateVersion();
    }

    /// <summary>Takes back earned points; when the customer has already spent them the balance goes to zero and the difference is the shortfall.</summary>
    public int ReverseAccrual(int points)
    {
        RequirePositive(points);
        var taken = Math.Min(points, Balance);
        Balance -= taken;
        UpdateVersion();
        return points - taken;
    }

    /// <summary>A merged-away customer hands its whole balance over (ADR-192); returns what moved.</summary>
    public int TransferOut()
    {
        var moved = Balance;
        Balance = 0;
        UpdateVersion();
        return moved;
    }

    public void TransferIn(int points)
    {
        RequirePositive(points);
        Balance = checked(Balance + points);
        UpdateVersion();
    }

    private static void RequirePositive(int points)
    {
        if (points <= 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "Points must be a positive number.", nameof(points));
    }
}
