using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-BRD-001 section 8.17 (ADR-236, renumbered ADR-369) -- a signature is DATA, not a file: bounded vector strokes (<c>[[[x,y],...],...]</c>, integer pairs 0..10000,
/// at most 32 KB serialised) for a drawn signature, or a typed name plus an explicit confirmation (the accessible alternative, UI-6). No object storage is needed.
/// </summary>
public sealed record SignatureCapture(string SignerName, SignatureMethod Method, string? StrokesJson, bool ConfirmedStatement, DateTimeOffset SignedOn)
{
    /// <summary>The reason this signature is invalid, or <c>null</c> when it is acceptable.</summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(SignerName) || SignerName.Length > BoardingSignatureRules.MaxSignerNameLength) return "A signer name is required.";
        if (!ConfirmedStatement) return "The signer must confirm the statement.";

        if (Method == SignatureMethod.Typed) return StrokesJson is null ? null : "A typed signature carries no strokes.";

        if (string.IsNullOrWhiteSpace(StrokesJson)) return "A drawn signature needs strokes.";
        if (System.Text.Encoding.UTF8.GetByteCount(StrokesJson) > BoardingSignatureRules.MaxSerialisedBytes) return "The signature is too large.";

        try
        {
            using var document = JsonDocument.Parse(StrokesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0) return "A drawn signature needs strokes.";

            foreach (var stroke in document.RootElement.EnumerateArray())
            {
                if (stroke.ValueKind != JsonValueKind.Array) return "Strokes must be lists of points.";
                foreach (var point in stroke.EnumerateArray())
                {
                    if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() != 2) return "A point is a pair of numbers.";
                    foreach (var coordinate in point.EnumerateArray())
                    {
                        if (coordinate.ValueKind != JsonValueKind.Number || !coordinate.TryGetInt32(out var value) || value < 0 || value > BoardingSignatureRules.MaxCoordinate)
                            return "Coordinates are whole numbers from 0 to 10000.";
                    }
                }
            }
        }
        catch (JsonException)
        {
            return "The signature is not valid.";
        }

        return null;
    }
}
