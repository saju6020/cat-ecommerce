namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-EMR-001 §8.9 -- bounds of an on-screen signature (stroke data, not an image).</summary>
public static class SignatureRules
{
    public const int MaxStrokes = 200;
    public const int MinPointsPerStroke = 2;
    public const int MinTotalPoints = 10;
    public const int MaxTotalPoints = 5000;
    public const int MaxDecimals = 4;
}
