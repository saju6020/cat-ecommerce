using CatEcommerce.Shared.Common.Helpers;
using Xunit;

namespace CatEcommerce.Domain.Tests.Helpers;

/// <summary>HO-NTF-001 §15 U-D2.</summary>
public sealed class DestinationMaskTests
{
    [Fact]
    public void Mobile_KeepsFirstSixAndLastThree()
    {
        Assert.Equal("+88017•••••678", DestinationMask.Mask("+8801712345678"));
    }

    [Fact]
    public void Email_KeepsFirstLocalCharacter_FixedThreeBullets()
    {
        Assert.Equal("r•••@example.com", DestinationMask.Mask("robert@example.com"));
    }

    [Fact]
    public void Email_OneLetterLocalPart_MaskedTheSameAsLongerOne()
    {
        var oneLetterMasked = DestinationMask.Mask("a@example.com");
        var tenLetterMasked = DestinationMask.Mask("abcdefghij@example.com");

        Assert.Equal("a•••@example.com", oneLetterMasked);
        Assert.Equal("a•••@example.com", tenLetterMasked);
    }
}
