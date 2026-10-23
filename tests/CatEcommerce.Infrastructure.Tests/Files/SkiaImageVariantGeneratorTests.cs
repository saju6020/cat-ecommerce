using CatEcommerce.Infrastructure.Files;
using CatEcommerce.Shared.Common.Constants;
using SkiaSharp;

namespace CatEcommerce.Infrastructure.Tests.Files;

/// <summary>ADR-370: the resized WebP copies of a product photo.</summary>
public sealed class SkiaImageVariantGeneratorTests
{
    private static readonly IReadOnlyList<FileVariant> Both = [FileVariants.Card, FileVariants.Detail];

    [Fact]
    public void A_large_photo_becomes_a_480_and_a_1200_pixel_webp_keeping_its_shape()
    {
        var copies = new SkiaImageVariantGenerator().Generate(new MemoryStream(Png(2400, 1200)), Both)!;

        Assert.Collection(copies,
            card => AssertWebp(card.Content, 480, 240),
            detail => AssertWebp(detail.Content, 1200, 600));
    }

    [Fact]
    public void A_small_photo_is_never_enlarged()
    {
        var copies = new SkiaImageVariantGenerator().Generate(new MemoryStream(Png(300, 200)), Both)!;

        Assert.All(copies, c => AssertWebp(c.Content, 300, 200));
    }

    [Fact]
    public void A_transparent_background_stays_transparent()
    {
        var copy = new SkiaImageVariantGenerator().Generate(new MemoryStream(Png(1000, 1000, transparentCorner: true)), [FileVariants.Card])!.Single();

        using var bitmap = SKBitmap.Decode(copy.Content);
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
        Assert.Equal(255, bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2).Alpha);
    }

    [Fact]
    public void A_phone_photo_taken_sideways_is_turned_upright_and_loses_its_exif()
    {
        // A 200x100 JPEG whose EXIF says "rotate 90 degrees clockwise to view" (orientation 6), as phones write it.
        var jpeg = WithExifOrientation(Jpeg(200, 100), orientation: 6);

        var copy = new SkiaImageVariantGenerator().Generate(new MemoryStream(jpeg), [FileVariants.Card])!.Single();

        AssertWebp(copy.Content, 100, 200);
        var exif = "Exif"u8.ToArray();
        Assert.False(Windows(copy.Content, exif.Length).Any(w => w.SequenceEqual(exif)), "the copy still carries EXIF");
    }

    [Fact]
    public void Bytes_that_are_not_an_image_give_null_so_the_upload_is_refused()
    {
        Assert.Null(new SkiaImageVariantGenerator().Generate(new MemoryStream("not an image at all"u8.ToArray()), Both));
    }

    // Gate fix (CODE_REVIEW LOW): a cut-off upload is refused, not stored with a half-grey copy.
    [Fact]
    public void A_truncated_photo_gives_null_so_the_upload_is_refused()
    {
        var jpeg = Jpeg(800, 600);

        Assert.Null(new SkiaImageVariantGenerator().Generate(new MemoryStream(jpeg[..(jpeg.Length / 2)]), Both));
    }

    // SECURITY gate (MEDIUM): a tiny file can declare enormous dimensions; it is refused before any pixels are allocated.
    [Fact]
    public void A_small_png_declaring_20000_by_20000_pixels_is_refused_before_decoding()
    {
        var bomb = PngHeaderOnly(20_000, 20_000);
        Assert.True(bomb.Length < 100);

        Assert.Null(new SkiaImageVariantGenerator().Generate(new MemoryStream(bomb), Both));
    }

    /// <summary>A PNG signature, a valid IHDR for the given size (RGB, 8-bit), an empty IDAT and IEND, with real CRCs.</summary>
    private static byte[] PngHeaderOnly(int width, int height)
    {
        static byte[] Chunk(string type, byte[] data)
        {
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            var length = new[] { (byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length };
            var crc = Crc32([.. typeBytes, .. data]);
            return [.. length, .. typeBytes, .. data, (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc];
        }
        byte[] ihdr = [(byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
            (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height, 8, 2, 0, 0, 0];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        return [.. signature, .. Chunk("IHDR", ihdr), .. Chunk("IDAT", [0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01]), .. Chunk("IEND", [])];
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }

    private static void AssertWebp(byte[] content, int width, int height)
    {
        Assert.Equal("RIFF"u8.ToArray(), content[..4]);
        Assert.Equal("WEBP"u8.ToArray(), content[8..12]);
        using var codec = SKCodec.Create(new MemoryStream(content));
        Assert.Equal(width, codec.Info.Width);
        Assert.Equal(height, codec.Info.Height);
    }

    private static byte[] Png(int width, int height, bool transparentCorner = false) =>
        Encode(width, height, SKEncodedImageFormat.Png, transparentCorner);

    private static byte[] Jpeg(int width, int height) => Encode(width, height, SKEncodedImageFormat.Jpeg, false);

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format, bool transparentCorner)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(transparentCorner ? SKColors.Transparent : SKColors.White);
            using var paint = new SKPaint { Color = new SKColor(21, 96, 90) };
            canvas.DrawRect(width / 4f, height / 4f, width / 2f, height / 2f, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(format, 90).ToArray();
    }

    /// <summary>Inserts a minimal APP1 "Exif" segment (big-endian TIFF, one Orientation tag) right after the JPEG's SOI.</summary>
    private static byte[] WithExifOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] tiff =
        [
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, // header, IFD0 at offset 8
            0x00, 0x01,                                               // one entry
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01,           // tag 0x0112 Orientation, SHORT, count 1
            (byte)(orientation >> 8), (byte)orientation, 0x00, 0x00,  // value
            0x00, 0x00, 0x00, 0x00,                                   // no next IFD
        ];
        var payload = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var length = payload.Length + 2;
        var segment = new byte[] { 0xFF, 0xE1, (byte)(length >> 8), (byte)length }.Concat(payload);
        return jpeg[..2].Concat(segment).Concat(jpeg[2..]).ToArray();
    }

    private static IEnumerable<byte[]> Windows(byte[] bytes, int size) =>
        Enumerable.Range(0, Math.Max(0, bytes.Length - size + 1)).Select(i => bytes[i..(i + size)]);
}
