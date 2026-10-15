using System.Globalization;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>HO-PHM-001 §8.3 -- a prescribed dose: an amount (positive, at most four decimal places) and its unit. UI-8: a dose never travels without its unit.</summary>
public sealed record Dose
{
    public decimal Amount { get; init; }
    public DoseUnit Unit { get; init; }

    public Dose(decimal amount, DoseUnit unit)
    {
        if (amount <= 0m || decimal.Round(amount, 4) != amount || amount > 100000m)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidDose, "The dose must be greater than zero with at most four decimal places.", nameof(Amount));

        Amount = amount;
        Unit = unit;
    }

    // EF only.
    private Dose() { Unit = default!; }

    public string AmountText => Amount.ToString("0.####", CultureInfo.InvariantCulture);
}
