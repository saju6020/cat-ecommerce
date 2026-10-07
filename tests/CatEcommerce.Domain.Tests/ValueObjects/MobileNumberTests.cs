using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Domain.Tests.ValueObjects;

/// <summary>HO-ACC-001 §15, U-A1/U-A2.</summary>
public sealed class MobileNumberTests
{
    // U-A1
    [Theory]
    [InlineData("01712345678")]
    [InlineData("+880 1712-345678")]
    [InlineData("008801712345678")]
    public void Create_WithEquivalentFormats_NormalisesToSameValue(string raw)
    {
        var mobile = MobileNumber.Create(raw);
        Assert.Equal("+8801712345678", mobile.Value);
    }

    [Fact]
    public void Create_WithEquivalentFormats_AreEqualAndSameHashCode()
    {
        var a = MobileNumber.Create("01712345678");
        var b = MobileNumber.Create("+880 1712-345678");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    // U-A2
    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0171234")]
    [InlineData("017123456789012345678")]
    public void Create_WithInvalidInput_Throws(string raw)
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => MobileNumber.Create(raw));
        Assert.Equal("InvalidMobileNumber", ex.ErrorCode);
    }
}
