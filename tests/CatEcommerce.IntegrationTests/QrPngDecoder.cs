using System.IO.Compression;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// ACCEPTANCE gate A22 fix, 2026-09-27. Decodes the exact, narrow PNG shape
/// <c>QRCoder.PngByteQRCode.GetGraphic</c> (Infrastructure's <c>QrCoderCatQrCodeGenerator</c>)
/// always produces -- non-interlaced, 1-bit-per-pixel grayscale, no palette -- into a luminance
/// byte array ZXing.Net can read, so a cage-card test can prove the returned PNG actually decodes
/// to a given URL rather than only asserting the HTTP envelope (200/image/png/non-empty).
///
/// Written against the BCL only (<see cref="ZLibStream"/>, .NET 6+) rather than pulling in a
/// general-purpose image library for the test project: every general-purpose managed image
/// library evaluated (SixLabors.ImageSharp) carries the "Six Labors Split License" -- free only
/// under specific criteria (open source, a transitive dependency, or a for-profit entity under
/// $1M USD annual revenue) -- the same class of licence condition that needed an explicit owner
/// decision for QuestPDF (HO-CAT-001 §20.6) before this codebase adopted it. Getting that decision
/// was out of scope for this fix, so this decoder avoids the question entirely: it depends on
/// nothing but the runtime and ZXing.Net (Apache-2.0, no commercial tier), and only ever needs to
/// understand the one fixed format QRCoder emits, not PNG in general.
/// </summary>
internal static class QrPngDecoder
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Decodes a 1-bit grayscale, non-interlaced PNG into a row-major grayscale luminance
    /// array (0 = black, 255 = white), plus its width and height.</summary>
    public static (byte[] Luminance, int Width, int Height) DecodeGrayscale1Bit(byte[] png)
    {
        if (png.Length < Signature.Length || !png.AsSpan(0, Signature.Length).SequenceEqual(Signature))
            throw new InvalidOperationException("Not a PNG file (bad signature).");

        int width = 0, height = 0, bitDepth = 0, colorType = -1, interlace = 0;
        using var idat = new MemoryStream();

        var offset = Signature.Length;
        while (offset < png.Length)
        {
            var length = ReadUInt32BigEndian(png, offset);
            var type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            var dataStart = offset + 8;

            switch (type)
            {
                case "IHDR":
                    width = (int)ReadUInt32BigEndian(png, dataStart);
                    height = (int)ReadUInt32BigEndian(png, dataStart + 4);
                    bitDepth = png[dataStart + 8];
                    colorType = png[dataStart + 9];
                    interlace = png[dataStart + 12];
                    break;
                case "IDAT":
                    idat.Write(png, dataStart, (int)length);
                    break;
                case "IEND":
                    offset = png.Length;
                    continue;
            }

            offset = dataStart + (int)length + 4; // + 4-byte CRC
        }

        if (bitDepth != 1 || colorType != 0 || interlace != 0)
            throw new NotSupportedException(
                $"QrPngDecoder only supports non-interlaced, 1-bit grayscale PNGs (got bitDepth={bitDepth}, colorType={colorType}, interlace={interlace}).");

        idat.Position = 0;
        using var zlibStream = new ZLibStream(idat, CompressionMode.Decompress);
        using var inflated = new MemoryStream();
        zlibStream.CopyTo(inflated);
        var raw = inflated.ToArray();

        var bytesPerRow = (width + 7) / 8; // 1 bit per pixel, packed MSB-first, byte-aligned per row
        var stride = bytesPerRow + 1; // +1 for the per-row filter-type byte
        var previousRow = new byte[bytesPerRow];
        var luminance = new byte[width * height];

        for (var y = 0; y < height; y++)
        {
            var rowStart = y * stride;
            var filterType = raw[rowStart];
            var currentRow = new byte[bytesPerRow];
            Array.Copy(raw, rowStart + 1, currentRow, 0, bytesPerRow);

            Unfilter(filterType, currentRow, previousRow);

            for (var x = 0; x < width; x++)
            {
                var bit = (currentRow[x / 8] >> (7 - (x % 8))) & 1;
                luminance[y * width + x] = bit == 0 ? (byte)0 : (byte)255; // 0 = black in grayscale PNG
            }

            previousRow = currentRow;
        }

        return (luminance, width, height);
    }

    /// <summary>PNG filter reconstruction (spec §9.2-9.4), bpp = 1 byte -- correct for every bit
    /// depth below 8 (1, 2 and 4), not only bit depth 1: the spec defines bpp for filtering as
    /// ceil(bitDepth * channels / 8), floored at 1.</summary>
    private static void Unfilter(byte filterType, byte[] current, byte[] previous)
    {
        const int bpp = 1;
        for (var i = 0; i < current.Length; i++)
        {
            int a = i >= bpp ? current[i - bpp] : 0; // left
            int b = previous[i]; // up
            int c = i >= bpp ? previous[i - bpp] : 0; // upper-left

            current[i] = filterType switch
            {
                0 => current[i],
                1 => (byte)(current[i] + a),
                2 => (byte)(current[i] + b),
                3 => (byte)(current[i] + (a + b) / 2),
                4 => (byte)(current[i] + PaethPredictor(a, b, c)),
                _ => throw new NotSupportedException($"Unknown PNG filter type {filterType}.")
            };
        }
    }

    private static int PaethPredictor(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }

    private static uint ReadUInt32BigEndian(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) | ((uint)buffer[offset + 2] << 8) | buffer[offset + 3];
}
