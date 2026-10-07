using CatEcommerce.Infrastructure.Security;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>HO-ACC-001 §15, U-B15.</summary>
public sealed class LocalListBreachedPasswordCheckerTests
{
    [Theory]
    [InlineData("password123")]
    [InlineData("PASSWORD123")]
    [InlineData("1234567890")]
    public async Task IsBreachedAsync_KnownCommonPassword_ReturnsTrue(string password)
    {
        var checker = new LocalListBreachedPasswordChecker();
        Assert.True(await checker.IsBreachedAsync(password));
    }

    [Fact]
    public async Task IsBreachedAsync_RandomTwentyCharPassword_ReturnsFalse()
    {
        var checker = new LocalListBreachedPasswordChecker();
        Assert.False(await checker.IsBreachedAsync("Zq7!kP2x@Lm9#Wv4Rt8Y"));
    }
}
