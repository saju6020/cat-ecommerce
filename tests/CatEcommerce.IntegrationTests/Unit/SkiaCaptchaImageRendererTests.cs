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

    // Production fix 2026-10-18: the Linux container has no fonts, so the system default face drew nothing.
    [Fact]
    public void Glyphs_UseTheEmbeddedFont_NotASystemFont()
    {
        Assert.Equal("Noto Sans", SkiaCaptchaImageRenderer.GlyphFontFamily);
    }

    [Fact]
    public void Render_DrawsTheCharacters_NotOnlyTheNoise()
    {
        var bytes = new SkiaCaptchaImageRenderer().Render("AB12X");
        using var bitmap = SkiaSharp.SKBitmap.Decode(bytes);

        // Glyphs are near-black (20,20,20); the dots and lines are mid grey (190 and 140), so dark pixels are glyph ink.
        var dark = 0;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Red < 80) dark++;

        Assert.True(dark > 400, $"only {dark} glyph pixels: the characters were not drawn");
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
