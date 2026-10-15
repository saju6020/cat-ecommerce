using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-IMP-001 §11.3 / ADR-106 -- a cost per STOCK UNIT: <c>decimal</c> to four places (never <c>double</c>), &gt;= 0,
/// with an ISO-4217 code. Deliberately not <see cref="Money"/>: HO-ADM-001 ADR-054 fixes <c>Money</c> at two places for
/// amounts charged; a per-unit cost is a rate, and rounding it to two would corrupt landed cost.
/// </summary>
public sealed record UnitCost
{
    public const int Scale = 4;

    public decimal Amount { get; }
    public string CurrencyCode { get; }

    public UnitCost(decimal amount, string currencyCode)
    {
        if (amount < 0m)
            throw new DomainRuleViolationException(BusinessViolationCodes.UnitCostRequired, "A unit cost cannot be negative.", nameof(Amount));
        if (!InventoryRules.IsValidCurrencyCode(currencyCode))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidCurrencyCode, "The currency must be a three-letter ISO-4217 code.", nameof(CurrencyCode));

        Amount = decimal.Round(amount, Scale, MidpointRounding.ToEven);
        CurrencyCode = currencyCode;
    }
}
