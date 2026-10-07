using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-XCT-001 §8.8, ADR-302. Locks in the three-outcome shape every IPersonalDataEraser
/// returns, since it is matched on by the (not-yet-built) retention worker.</summary>
public sealed class ErasureOutcomeTests
{
    [Fact]
    public void Completed_IsDistinguishableByPatternMatching()
    {
        ErasureOutcome outcome = new ErasureOutcome.Completed();

        var description = Describe(outcome);

        Assert.Equal("completed", description);
    }

    [Fact]
    public void Held_CarriesUntilAndBasis()
    {
        var until = new DateOnly(2027, 1, 1);
        ErasureOutcome outcome = new ErasureOutcome.Held(until, "D-9 invoice");

        var held = Assert.IsType<ErasureOutcome.Held>(outcome);
        Assert.Equal(until, held.Until);
        Assert.Equal("D-9 invoice", held.Basis);
        Assert.Equal("held", Describe(outcome));
    }

    [Fact]
    public void Failed_CarriesErrorCode()
    {
        ErasureOutcome outcome = new ErasureOutcome.Failed("UnhandledEraserException");

        var failed = Assert.IsType<ErasureOutcome.Failed>(outcome);
        Assert.Equal("UnhandledEraserException", failed.ErrorCode);
        Assert.Equal("failed", Describe(outcome));
    }

    [Fact]
    public void Held_WithSameValues_AreEqual()
    {
        var until = new DateOnly(2027, 1, 1);

        Assert.Equal(new ErasureOutcome.Held(until, "D-9 invoice"), new ErasureOutcome.Held(until, "D-9 invoice"));
    }

    private static string Describe(ErasureOutcome outcome) => outcome switch
    {
        ErasureOutcome.Completed => "completed",
        ErasureOutcome.Held => "held",
        ErasureOutcome.Failed => "failed",
        _ => throw new InvalidOperationException("Unreachable"),
    };
}
