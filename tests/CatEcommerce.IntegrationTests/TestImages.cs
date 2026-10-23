using SkiaSharp;

namespace CatEcommerce.IntegrationTests;

/// <summary>ADR-370: product-photo uploads now make resized copies, so a test upload must be a real, decodable image
/// (a header-only "PNG" passes the magic-byte check but is refused as "could not be processed").</summary>
internal static class TestImages
{
    /// <summary>A real 4x4 PNG with the right signature.</summary>
    public static byte[] Png()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(4, 4, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(new SKColor(21, 96, 90));
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }
}
