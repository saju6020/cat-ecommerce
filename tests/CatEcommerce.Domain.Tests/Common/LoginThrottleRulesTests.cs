using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-ACC-001 §15, U-A4.</summary>
public sealed class LoginThrottleRulesTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 5)]
    [InlineData(4, 15)]
    [InlineData(5, 60)]
    [InlineData(6, 300)]
    [InlineData(7, 900)]
    [InlineData(20, 900)]
    public void DelayFor_ReturnsExpectedSeconds(int failures, int expectedSeconds) =>
        Assert.Equal(expectedSeconds, LoginThrottleRules.DelayFor(failures));

    [Fact]
    public void DelayFor_IsMonotonicNonDecreasing()
    {
        var previous = 0;
        for (var i = 1; i <= 20; i++)
        {
            var delay = LoginThrottleRules.DelayFor(i);
            Assert.True(delay >= previous);
            previous = delay;
        }
    }
}
