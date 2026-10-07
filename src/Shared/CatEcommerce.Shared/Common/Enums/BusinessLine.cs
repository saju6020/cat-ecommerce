namespace CatEcommerce.Shared.Common.Enums;

/// <summary>Owned by HO-BIL-001 §8.2/§10.1. Revenue by line of business (SRS §8.2, US-RPT-03).</summary>
public sealed class BusinessLine(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BusinessLine Clinic = new(1, nameof(Clinic));
    public static readonly BusinessLine Shop = new(2, nameof(Shop));
    public static readonly BusinessLine Boarding = new(3, nameof(Boarding));
}
