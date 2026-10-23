using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using SkiaSharp;

namespace CatEcommerce.Infrastructure.Files;

/// <summary>
/// ADR-370: the resized WebP copies of an uploaded image (<see cref="FileVariants"/>). A copy is never larger than its
/// original (no upscaling) and keeps transparency. The camera's rotation (EXIF orientation) is applied to the pixels and
/// no metadata is written, so a copy carries no EXIF or GPS data (HO-FIL-001 OQ-4, for the copies). Large photos are
/// decoded at a reduced scale where the codec allows it (JPEG), keeping memory bounded for 5 MB uploads.
/// </summary>
public sealed class SkiaImageVariantGenerator : IImageVariantGenerator
{
    internal const int WebpQuality = 80;

    /// <summary>Gate fix (CODE_REVIEW MEDIUM): a small file can declare huge dimensions; nothing larger is decoded.</summary>
    internal const long MaxDecodedPixels = 50_000_000;

    public IReadOnlyList<GeneratedVariant>? Generate(Stream image, IReadOnlyList<FileVariant> variants)
    {
        if (variants.Count == 0) return [];
        if (image.CanSeek) image.Position = 0;

        using var data = SKData.Create(image);
        if (data is null) return null;
        using var codec = SKCodec.Create(data);
        if (codec is null) return null;

        // Decode no larger than twice the biggest copy needs: plenty for a sharp downscale, a quarter of the memory.
        var largest = variants.Max(v => v.MaxPixels);
        var longest = Math.Max(codec.Info.Width, codec.Info.Height);
        var scale = longest > largest * 2 ? (float)largest * 2 / longest : 1f;
        var size = codec.GetScaledDimensions(scale);
        if ((long)size.Width * size.Height > MaxDecodedPixels) return null;
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var decoded = new SKBitmap(info);
        // A truncated file (IncompleteInput) is refused rather than stored with a half-grey copy.
        if (codec.GetPixels(info, decoded.GetPixels()) != SKCodecResult.Success) return null;

        using var turned = Upright(decoded, codec.EncodedOrigin);
        var upright = turned ?? decoded;
        var copies = new List<GeneratedVariant>(variants.Count);
        foreach (var variant in variants)
        {
            var fit = Math.Min(1f, (float)variant.MaxPixels / Math.Max(upright.Width, upright.Height));
            using var resized = fit < 1f ? Shrink(upright, fit) : null;
            if (fit < 1f && resized is null) return null;
            using var encodedImage = SKImage.FromBitmap(resized ?? upright);
            using var webp = encodedImage.Encode(SKEncodedImageFormat.Webp, WebpQuality);
            if (webp is null) return null;
            copies.Add(new GeneratedVariant(variant, webp.ToArray()));
        }

        return copies;
    }

    /// <summary>
    /// Gate fix (CODE_REVIEW LOW): a single cubic resize from 2,400 px to 480 px aliases, so the image is halved with
    /// linear filtering until it is within twice the target, then finished with one Mitchell pass.
    /// </summary>
    private static SKBitmap? Shrink(SKBitmap source, float fit)
    {
        var targetWidth = Math.Max(1, (int)Math.Round(source.Width * fit));
        var targetHeight = Math.Max(1, (int)Math.Round(source.Height * fit));
        SKBitmap? current = null;
        try
        {
            while ((current ?? source).Width > targetWidth * 2)
            {
                var from = current ?? source;
                var half = from.Resize(new SKImageInfo(Math.Max(targetWidth, from.Width / 2), Math.Max(targetHeight, from.Height / 2),
                    SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear));
                current?.Dispose();
                current = half;
                if (current is null) return null;
            }
            var result = (current ?? source).Resize(new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKCubicResampler.Mitchell));
            return result;
        }
        finally
        {
            current?.Dispose();
        }
    }

    /// <summary>Draws the pixels the right way up for the EXIF orientation (x' = sx*x + kx*y + tx, y' = ky*x + sy*y + ty);
    /// null when they already are (no copy is made).</summary>
    private static SKBitmap? Upright(SKBitmap source, SKEncodedOrigin origin)
    {
        float w = source.Width, h = source.Height;
        var (matrix, swap) = origin switch
        {
            SKEncodedOrigin.TopRight => (new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1), false),
            SKEncodedOrigin.BottomRight => (new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1), false),
            SKEncodedOrigin.BottomLeft => (new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1), false),
            SKEncodedOrigin.LeftTop => (new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1), true),
            SKEncodedOrigin.RightTop => (new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1), true),
            SKEncodedOrigin.RightBottom => (new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1), true),
            SKEncodedOrigin.LeftBottom => (new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1), true),
            _ => (SKMatrix.Identity, false),
        };
        if (matrix == SKMatrix.Identity) return null;

        var turned = new SKBitmap(new SKImageInfo(swap ? source.Height : source.Width, swap ? source.Width : source.Height,
            SKColorType.Rgba8888, SKAlphaType.Premul));
        try
        {
            using var canvas = new SKCanvas(turned);
            canvas.Clear(SKColors.Transparent);
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(source, 0, 0);
            return turned;
        }
        catch
        {
            turned.Dispose();
            throw;
        }
    }
}
