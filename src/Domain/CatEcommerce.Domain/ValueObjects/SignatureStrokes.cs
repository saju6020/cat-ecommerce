using System.Globalization;
using System.Text;
using System.Text.Json;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-EMR-001 §8.9 / ADR-132 -- an on-screen signature as <b>stroke data, not an image</b>:
/// <c>strokes: number[][][]</c>, each stroke a list of <c>[x, y]</c> points in <c>[0, 1]</c> with at
/// most four decimals. 1 to 200 strokes, each of at least 2 points, 10 to 5000 points in total --
/// anything else is <c>InvalidSignature</c>. Being data, it needs no upload and no malware scan, and
/// the server renders it to SVG from numbers alone (<see cref="ToSvg"/>), so signing keeps working
/// where uploads are refused.
/// </summary>
public sealed record SignatureStrokes
{
    public IReadOnlyList<IReadOnlyList<(decimal X, decimal Y)>> Strokes { get; }

    private SignatureStrokes(IReadOnlyList<IReadOnlyList<(decimal X, decimal Y)>> strokes) => Strokes = strokes;

    public int TotalPoints => Strokes.Sum(s => s.Count);

    /// <summary>Validates and builds. <paramref name="strokes"/> is the wire shape: strokes of
    /// points, each point exactly two numbers.</summary>
    public static SignatureStrokes Create(decimal[][][]? strokes)
    {
        if (strokes is null || strokes.Length < 1 || strokes.Length > SignatureRules.MaxStrokes)
            throw Invalid($"A signature has between 1 and {SignatureRules.MaxStrokes} strokes.");

        var result = new List<IReadOnlyList<(decimal X, decimal Y)>>(strokes.Length);
        var total = 0;

        foreach (var stroke in strokes)
        {
            if (stroke is null || stroke.Length < SignatureRules.MinPointsPerStroke)
                throw Invalid($"Each stroke needs at least {SignatureRules.MinPointsPerStroke} points.");

            var points = new List<(decimal X, decimal Y)>(stroke.Length);
            foreach (var point in stroke)
            {
                if (point is null || point.Length != 2)
                    throw Invalid("Each point is exactly two numbers, [x, y].");

                var (x, y) = (point[0], point[1]);
                if (!IsUnitCoordinate(x) || !IsUnitCoordinate(y))
                    throw Invalid($"Coordinates must be between 0 and 1 with at most {SignatureRules.MaxDecimals} decimals.");

                points.Add((x, y));
            }

            total += points.Count;
            if (total > SignatureRules.MaxTotalPoints)
                throw Invalid($"A signature has at most {SignatureRules.MaxTotalPoints} points.");

            result.Add(points);
        }

        if (total < SignatureRules.MinTotalPoints)
            throw Invalid($"A signature needs at least {SignatureRules.MinTotalPoints} points.");

        return new SignatureStrokes(result);
    }

    public string ToJson()
    {
        var wire = Strokes.Select(s => s.Select(p => new[] { p.X, p.Y }).ToArray()).ToArray();
        return JsonSerializer.Serialize(wire);
    }

    /// <summary>Rehydrates a stored signature. Stored data was validated on the way in, so this only
    /// parses; a corrupt value fails loudly rather than being tolerated.</summary>
    public static SignatureStrokes FromJson(string json)
    {
        var wire = JsonSerializer.Deserialize<decimal[][][]>(json)
            ?? throw new InvalidOperationException("Stored signature is empty.");
        return Create(wire);
    }

    /// <summary>
    /// Renders the strokes as an SVG made of <c>&lt;polyline&gt;</c> elements and nothing else --
    /// the only text interpolated is numbers formatted with the invariant culture, so no stored
    /// value can inject markup or script.
    /// </summary>
    public string ToSvg(int size = 300)
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\">");

        foreach (var stroke in Strokes)
        {
            builder.Append("<polyline fill=\"none\" stroke=\"#000000\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" points=\"");
            builder.Append(string.Join(' ', stroke.Select(p =>
                string.Create(CultureInfo.InvariantCulture, $"{(p.X * size):0.##},{(p.Y * size):0.##}"))));
            builder.Append("\"/>");
        }

        builder.Append("</svg>");
        return builder.ToString();
    }

    private static bool IsUnitCoordinate(decimal value) =>
        value >= 0m && value <= 1m && decimal.Round(value, SignatureRules.MaxDecimals) == value;

    private static DomainRuleViolationException Invalid(string message) =>
        new(BusinessViolationCodes.InvalidSignature, message, "Signature");
}
