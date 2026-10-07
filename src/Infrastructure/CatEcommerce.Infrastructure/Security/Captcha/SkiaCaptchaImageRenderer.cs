using SkiaSharp;

namespace CatEcommerce.Infrastructure.Security.Captcha;

/// <summary>
/// HO-ACC-001 §12.6 / §20.13. PNG 200x70, glyph/background contrast >= 4.5:1, per-glyph rotation
/// and offset, crossing lines, dot noise. Not SVG (machine-readable). The randomness here is
/// cosmetic (line/dot placement) -- the answer itself is generated with
/// <see cref="System.Security.Cryptography.RandomNumberGenerator"/> by <see cref="MemoryCacheCaptchaService"/>,
/// never <see cref="System.Random"/>.
/// </summary>
public sealed class SkiaCaptchaImageRenderer : ICaptchaImageRenderer
{
    private const int Width = 200;
    private const int Height = 70;

    public byte[] Render(string answer)
    {
        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var random = Random.Shared;

        using (var dotPaint = new SKPaint { Color = new SKColor(190, 190, 190), IsAntialias = true })
        {
            for (var i = 0; i < 140; i++)
                canvas.DrawCircle(random.Next(Width), random.Next(Height), 1, dotPaint);
        }

        using (var linePaint = new SKPaint { Color = new SKColor(140, 140, 140), StrokeWidth = 2, IsAntialias = true })
        {
            var lineCount = random.Next(4, 7);
            for (var i = 0; i < lineCount; i++)
                canvas.DrawLine(random.Next(Width), random.Next(Height), random.Next(Width), random.Next(Height), linePaint);
        }

        using var font = new SKFont(SKTypeface.Default, 42);
        using var textPaint = new SKPaint { Color = new SKColor(20, 20, 20), IsAntialias = true };

        var glyphWidth = Width / Math.Max(answer.Length, 1);
        for (var i = 0; i < answer.Length; i++)
        {
            canvas.Save();
            var x = (i * glyphWidth) + (glyphWidth / 2f);
            var y = (Height / 2f) + 14;
            canvas.Translate(x, y);
            canvas.RotateDegrees(random.Next(-20, 21));
            canvas.Translate(0, random.Next(-4, 5));
            canvas.DrawText(answer[i].ToString(), 0, 0, SKTextAlign.Center, font, textPaint);
            canvas.Restore();
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
