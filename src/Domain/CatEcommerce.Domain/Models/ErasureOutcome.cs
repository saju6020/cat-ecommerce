namespace CatEcommerce.Domain.Models;

/// <summary>
/// The result of one <c>IPersonalDataEraser.EraseAsync</c> call (HO-XCT-001 §8.8, ADR-302). Shape
/// transcribed verbatim from §8.8's own code block.
/// </summary>
public abstract record ErasureOutcome
{
    private ErasureOutcome()
    {
    }

    /// <summary>Nothing of this context's remains for the customer -- either there never was
    /// any, or every last held item's date has passed.</summary>
    public sealed record Completed : ErasureOutcome;

    /// <summary>Personal data survives inside a retained record until <paramref name="Until"/>
    /// (e.g. an invoice under D-9). <paramref name="Basis"/> is a short citation, e.g. "D-9
    /// invoice", never a sentence shown to a user.</summary>
    public sealed record Held(DateOnly Until, string Basis) : ErasureOutcome;

    /// <summary>A recoverable problem prevented erasure this attempt; the retention worker
    /// retries with back-off. <paramref name="ErrorCode"/> is a code, never a message or a
    /// value.</summary>
    public sealed record Failed(string ErrorCode) : ErasureOutcome;
}
