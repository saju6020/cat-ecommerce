using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-SHP-001 §8.3.1 -- owned by <c>ProductAggregate</c> (ADR-079). A stable id, never
/// reused and never deleted: discontinued instead. Field lengths and the pack-quantity range are
/// shape validation (the command validator's job, §11.2); this entity enforces the two rules that
/// are genuinely domain invariants regardless of entry point: the code pattern and the barcode
/// format/GTIN check digit (<c>InvalidBarcode</c>).</summary>
public sealed partial class Sku
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string? Barcode { get; private set; }
    public string? SizeOrWeight { get; private set; }
    public string? Flavour { get; private set; }
    public string? Colour { get; private set; }
    public int? PackQuantity { get; private set; }
    public SkuStatus Status { get; private set; } = SkuStatus.Active;

    /// <summary>Derived display label -- the set axes joined with " · ", or the product name when
    /// none are set (the single-SKU / bundle case). The product name is supplied by the caller
    /// because this entity has no reference back to its owner.</summary>
    public string Label(string productName) => LabelFor(productName, SizeOrWeight, Flavour, Colour, PackQuantity);

    /// <summary>The same label from projected columns -- so a read-side query can build it without
    /// materialising the aggregate (.ai/read-side-pattern.md section 4, "project, never
    /// rehydrate").</summary>
    public static string LabelFor(string productName, string? sizeOrWeight, string? flavour, string? colour, int? packQuantity)
    {
        var axes = new List<string>();
        if (!string.IsNullOrWhiteSpace(sizeOrWeight)) axes.Add(sizeOrWeight!);
        if (!string.IsNullOrWhiteSpace(flavour)) axes.Add(flavour!);
        if (!string.IsNullOrWhiteSpace(colour)) axes.Add(colour!);
        if (packQuantity is not null) axes.Add($"Pack of {packQuantity}");
        return axes.Count > 0 ? string.Join(" · ", axes) : productName;
    }

    /// <summary>Case-insensitive combination key, used by <c>ProductAggregate</c> to refuse a
    /// duplicate variant.</summary>
    public string VariantKey => string.Join("|",
        (SizeOrWeight ?? "").Trim().ToLowerInvariant(),
        (Flavour ?? "").Trim().ToLowerInvariant(),
        (Colour ?? "").Trim().ToLowerInvariant(),
        PackQuantity?.ToString() ?? "");

    public bool HasAnyAxis => !string.IsNullOrWhiteSpace(SizeOrWeight) || !string.IsNullOrWhiteSpace(Flavour)
                               || !string.IsNullOrWhiteSpace(Colour) || PackQuantity is not null;

    private Sku() { }

    public Sku(Guid id, string code, string? barcode, string? sizeOrWeight, string? flavour, string? colour, int? packQuantity)
    {
        Id = id;
        SetCode(code);
        SetBarcode(barcode);
        SizeOrWeight = Trim(sizeOrWeight);
        Flavour = Trim(flavour);
        Colour = Trim(colour);
        PackQuantity = packQuantity;
        Status = SkuStatus.Active;
    }

    public void UpdateAxes(string? sizeOrWeight, string? flavour, string? colour, int? packQuantity)
    {
        SizeOrWeight = Trim(sizeOrWeight);
        Flavour = Trim(flavour);
        Colour = Trim(colour);
        PackQuantity = packQuantity;
    }

    public void Discontinue() => Status = SkuStatus.Discontinued;

    public void Reactivate() => Status = SkuStatus.Active;

    private void SetCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > ProductRules.SkuCodeMaxLength || !SkuCodePattern().IsMatch(code))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidSkuCode, "SKU code must be upper-case letters, digits and single hyphens, up to 40 characters.", nameof(Code));

        Code = code;
    }

    private void SetBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            Barcode = null;
            return;
        }

        if (barcode.Length < ProductRules.BarcodeMinLength || barcode.Length > ProductRules.BarcodeMaxLength || !BarcodePattern().IsMatch(barcode))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBarcode, "Barcode must be 4-32 characters of digits, letters or hyphens.", nameof(Barcode));

        if (barcode.Length is 8 or 12 or 13 or 14 && barcode.All(char.IsDigit) && !Gtin.HasValidCheckDigit(barcode))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBarcode, "This looks like a GTIN barcode, but its check digit is not valid.", nameof(Barcode));

        Barcode = barcode;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Z0-9]+(-[A-Z0-9]+)*$")]
    private static partial System.Text.RegularExpressions.Regex SkuCodePattern();

    [System.Text.RegularExpressions.GeneratedRegex(@"^[0-9A-Za-z-]+$")]
    private static partial System.Text.RegularExpressions.Regex BarcodePattern();
}
