using CatEcommerce.Infrastructure.Security.Captcha;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>HO-ACC-001 §15, U-B12 / §20.13.</summary>
public sealed class SkiaCaptchaImageRendererTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void Render_ProducesValidPngNotContainingTheAnswerBytes()
    {
        var renderer = new SkiaCaptchaImageRenderer();
        var bytes = renderer.Render("AB12X");

        Assert.True(bytes.Length > PngSignature.Length);
        Assert.Equal(PngSignature, bytes[..8]);

        // The answer text does not appear as literal ASCII bytes in the compressed PNG stream.
        var answerBytes = System.Text.Encoding.ASCII.GetBytes("AB12X");
        Assert.False(Contains(bytes, answerBytes));
    }

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }
}
