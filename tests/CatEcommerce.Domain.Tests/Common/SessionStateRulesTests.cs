using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-ACC-001 §15, U-C8.</summary>
public sealed class SessionStateRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Stamp = Guid.NewGuid();

    [Fact]
    public void Classify_Ended_ReturnsEnded() =>
        Assert.Equal(SessionState.Ended, SessionStateRules.Classify(true, Now.AddHours(1), Now, Stamp, Stamp));

    [Fact]
    public void Classify_AtExpiry_ReturnsExpired() =>
        Assert.Equal(SessionState.Expired, SessionStateRules.Classify(false, Now, Now, Stamp, Stamp));

    [Fact]
    public void Classify_PastExpiry_ReturnsExpired() =>
        Assert.Equal(SessionState.Expired, SessionStateRules.Classify(false, Now.AddMinutes(-1), Now, Stamp, Stamp));

    [Fact]
    public void Classify_StampMismatch_ReturnsRevoked() =>
        Assert.Equal(SessionState.Revoked, SessionStateRules.Classify(false, Now.AddHours(1), Now, Stamp, Guid.NewGuid()));

    [Fact]
    public void Classify_Valid_ReturnsActive() =>
        Assert.Equal(SessionState.Active, SessionStateRules.Classify(false, Now.AddHours(1), Now, Stamp, Stamp));

    [Fact]
    public void Classify_Staff_IdleBeyondTimeout_ReturnsIdleExpired()
    {
        var state = SessionStateRules.Classify(
            false, Now.AddHours(1), Now, Stamp, Stamp, Now.AddMinutes(-31), TimeSpan.FromMinutes(30));

        Assert.Equal(SessionState.IdleExpired, state);
    }

    [Fact]
    public void Classify_Staff_WithinIdleTimeout_ReturnsActive()
    {
        var state = SessionStateRules.Classify(
            false, Now.AddHours(1), Now, Stamp, Stamp, Now.AddMinutes(-10), TimeSpan.FromMinutes(30));

        Assert.Equal(SessionState.Active, state);
    }
}
