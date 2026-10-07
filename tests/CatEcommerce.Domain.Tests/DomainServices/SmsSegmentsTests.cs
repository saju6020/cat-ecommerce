using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-NTF-001 §15 U-A12. Pure -- no database.</summary>
public sealed class SmsSegmentsTests
{
    [Fact]
    public void Gsm7_160Characters_OneSegment()
    {
        var (encoding, characters, segments) = SmsSegments.Measure(new string('a', 160));
        Assert.Equal(SmsEncoding.Gsm7, encoding);
        Assert.Equal(160, characters);
        Assert.Equal(1, segments);
    }

    [Fact]
    public void Gsm7_161Characters_TwoSegments()
    {
        var (encoding, _, segments) = SmsSegments.Measure(new string('a', 161));
        Assert.Equal(SmsEncoding.Gsm7, encoding);
        Assert.Equal(2, segments);
    }

    [Fact]
    public void Ucs2_70CharactersWithNonGsmChar_OneSegment()
    {
        var text = new string('a', 69) + "৳"; // Bengali currency sign (not in GSM-7)
        var (encoding, characters, segments) = SmsSegments.Measure(text);

        Assert.Equal(SmsEncoding.Ucs2, encoding);
        Assert.Equal(70, characters);
        Assert.Equal(1, segments);
    }

    [Fact]
    public void Ucs2_71Characters_TwoSegments()
    {
        var text = new string('a', 70) + "৳";
        var (encoding, _, segments) = SmsSegments.Measure(text);

        Assert.Equal(SmsEncoding.Ucs2, encoding);
        Assert.Equal(2, segments);
    }
}
