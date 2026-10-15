using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-IMP-001 §8.2 / §11.3 -- a strictly positive quantity in the SKU's stock unit, at most three decimal places
/// (stored <c>numeric(12,3)</c>), and whole unless the SKU allows fractions (ADR-111).
/// </summary>
public sealed record StockQuantity
{
    public const int Scale = 3;
    public const decimal Max = 999_999_999.999m;

    public decimal Value { get; }

    private StockQuantity(decimal value) => Value = value;

    public static StockQuantity Create(decimal value, bool allowsFractionalQuantity, string propertyName = "Quantity")
    {
        if (value <= 0m || value > Max || decimal.Round(value, Scale) != value)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidQuantity, "The quantity must be greater than zero, with at most three decimal places.", propertyName);

        if (!allowsFractionalQuantity && value != decimal.Truncate(value))
            throw new DomainRuleViolationException(BusinessViolationCodes.FractionalQuantityNotAllowed, "This item can only be counted in whole units.", propertyName);

        return new StockQuantity(value);
    }

    /// <summary>The non-throwing form, for handlers that collect every violation.</summary>
    public static bool TryValidate(decimal value, bool allowsFractionalQuantity, out string? code)
    {
        code = null;
        if (value <= 0m || value > Max || decimal.Round(value, Scale) != value) { code = BusinessViolationCodes.InvalidQuantity; return false; }
        if (!allowsFractionalQuantity && value != decimal.Truncate(value)) { code = BusinessViolationCodes.FractionalQuantityNotAllowed; return false; }
        return true;
    }
}
