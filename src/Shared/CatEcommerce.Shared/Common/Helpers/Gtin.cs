namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>HO-SHP-001 §8.3.1 -- GS1 check-digit validation for an 8/12/13/14-digit numeric
/// barcode (EAN-8/UPC-A/EAN-13/GTIN-14). The check digit is the last digit; weights alternate
/// 3,1,3,1... from the rightmost digit before it.</summary>
public static class Gtin
{
    public static bool HasValidCheckDigit(string digits)
    {
        if (digits.Length is not (8 or 12 or 13 or 14) || !digits.All(char.IsDigit)) return false;

        var sum = 0;
        // Walk from the digit just before the check digit, alternating weights 3, 1, 3, 1...
        for (var i = 0; i < digits.Length - 1; i++)
        {
            var digit = digits[digits.Length - 2 - i] - '0';
            var weight = i % 2 == 0 ? 3 : 1;
            sum += digit * weight;
        }

        var expectedCheckDigit = (10 - sum % 10) % 10;
        var actualCheckDigit = digits[^1] - '0';
        return expectedCheckDigit == actualCheckDigit;
    }
}
