using CatEcommerce.Shared.Common.Helpers;
using Xunit;

namespace CatEcommerce.Domain.Tests.Helpers;

/// <summary>HO-NTF-001 SECURITY/CODE_REVIEW gate fix (2026-09-27, MEDIUM 7). Reproduces the
/// renderer crash a literal sentinel character causes before the fix: an admin-authored template
/// or a request variable carrying U+E000/U+E001 (or any other Private-Use/Format code point) must
/// be rejected before it ever reaches <c>NotificationRenderer</c>.</summary>
public sealed class ControlCharactersTests
{
    [Theory]
    [InlineData("")]
    [InlineData("")]
    [InlineData("plain 99 text")]
    [InlineData("")] // last code point in the Basic Multilingual Plane private-use range
    [InlineData("​")] // zero-width space -- Format category
    public void ContainsPrivateUseOrFormat_DetectsSentinelAndOtherInvisibleCharacters(string value) =>
        Assert.True(ControlCharacters.ContainsPrivateUseOrFormat(value));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Mitu's Rabies vaccine is due 12 Jan 2027")]
    [InlineData("+8801917190142")]
    [InlineData("A café's café")] // ordinary accented text must not be flagged
    public void ContainsPrivateUseOrFormat_AllowsOrdinaryText(string? value) =>
        Assert.False(ControlCharacters.ContainsPrivateUseOrFormat(value));

    [Fact]
    public void ContainsPrivateUseOrFormat_DetectsSupplementaryPlanePrivateUse()
    {
        // U+F0000, a supplementary-plane private-use code point -- exercises the surrogate-pair path.
        var value = char.ConvertFromUtf32(0xF0000);

        Assert.True(ControlCharacters.ContainsPrivateUseOrFormat(value));
    }
}
