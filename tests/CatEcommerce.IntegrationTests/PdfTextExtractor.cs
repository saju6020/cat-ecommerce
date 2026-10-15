using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// A minimal PDF text reader for the tests (no PDF package may be added). SkiaSharp -- QuestPDF's backend -- writes text as
/// 2-byte glyph ids in hex strings (<c>&lt;0102&gt; Tj</c>) with each font's <c>/ToUnicode</c> CMap: this inflates the
/// streams, reads each CMap, and maps every shown glyph id back to its characters, so a test can assert that a printed
/// document really contains "INV-…" or a seller's tax registration number. Not a general PDF parser.
/// </summary>
internal static partial class PdfTextExtractor
{
    public static string Extract(byte[] pdf)
    {
        var raw = Encoding.Latin1.GetString(pdf);
        var objects = ParseObjects(raw, pdf);

        var cmapByFontObject = new Dictionary<int, Dictionary<int, string>>();
        foreach (var (id, obj) in objects)
        {
            if (!obj.Dictionary.Contains("/Type /Font") && !obj.Dictionary.Contains("/Type/Font")) continue;

            var toUnicode = ToUnicodeRef().Match(obj.Dictionary);
            if (toUnicode.Success && objects.TryGetValue(int.Parse(toUnicode.Groups[1].Value), out var cmapObject) && cmapObject.Stream is not null)
                cmapByFontObject[id] = ParseCMap(Encoding.Latin1.GetString(cmapObject.Stream));
        }

        // Font resource names (/F4 12 0 R) -> font object ids.
        var fontByName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var obj in objects.Values)
        {
            foreach (Match match in FontResource().Matches(obj.Dictionary))
                fontByName[match.Groups[1].Value] = int.Parse(match.Groups[2].Value);
        }

        var text = new StringBuilder();
        foreach (var obj in objects.Values)
        {
            if (obj.Stream is null) continue;

            var content = Encoding.Latin1.GetString(obj.Stream);
            if (!content.Contains(" Tf")) continue;

            Dictionary<int, string>? current = null;
            foreach (Match token in ContentToken().Matches(content))
            {
                if (token.Groups["font"].Success)
                {
                    current = fontByName.TryGetValue(token.Groups["font"].Value, out var fontId) && cmapByFontObject.TryGetValue(fontId, out var map) ? map : null;
                }
                else if (token.Groups["hex"].Success && current is not null)
                {
                    text.Append(Decode(token.Groups["hex"].Value, current));
                }
                else if (token.Groups["array"].Success && current is not null)
                {
                    foreach (Match hex in HexString().Matches(token.Groups["array"].Value)) text.Append(Decode(hex.Groups[1].Value, current));

                }
                else if (token.Groups["newline"].Success)
                {

                }
            }
        }

        return text.ToString();
    }

    private static string Decode(string hex, Dictionary<int, string> map)
    {
        var builder = new StringBuilder();
        for (var i = 0; i + 3 < hex.Length; i += 4)
        {
            var code = Convert.ToInt32(hex.Substring(i, 4), 16);
            if (map.TryGetValue(code, out var value)) builder.Append(value);
        }

        return builder.ToString();
    }

    private sealed record PdfObject(string Dictionary, byte[]? Stream);

    private static Dictionary<int, PdfObject> ParseObjects(string raw, byte[] bytes)
    {
        var result = new Dictionary<int, PdfObject>();
        foreach (Match match in ObjectStart().Matches(raw))
        {
            var id = int.Parse(match.Groups[1].Value);
            var bodyStart = match.Index + match.Length;
            var end = raw.IndexOf("endobj", bodyStart, StringComparison.Ordinal);
            if (end < 0) continue;

            var streamMarker = raw.IndexOf("stream", bodyStart, end - bodyStart, StringComparison.Ordinal);
            if (streamMarker < 0)
            {
                result[id] = new PdfObject(raw[bodyStart..end], null);
                continue;
            }

            var dictionary = raw[bodyStart..streamMarker];
            var dataStart = streamMarker + "stream".Length;
            if (dataStart < raw.Length && raw[dataStart] == '\r') dataStart++;
            if (dataStart < raw.Length && raw[dataStart] == '\n') dataStart++;

            var dataEnd = raw.LastIndexOf("endstream", end, StringComparison.Ordinal);
            if (dataEnd < dataStart) dataEnd = end;

            var slice = bytes[dataStart..dataEnd];
            result[id] = new PdfObject(dictionary, dictionary.Contains("FlateDecode") ? Inflate(slice) : slice);
        }

        return result;
    }

    private static byte[]? Inflate(byte[] data)
    {
        try
        {
            using var input = new MemoryStream(data);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static Dictionary<int, string> ParseCMap(string cmap)
    {
        var map = new Dictionary<int, string>();

        foreach (Match block in BfChar().Matches(cmap))
            foreach (Match pair in CharPair().Matches(block.Groups[1].Value))
                map[Convert.ToInt32(pair.Groups[1].Value, 16)] = FromUtf16Hex(pair.Groups[2].Value);

        foreach (Match block in BfRange().Matches(cmap))
        {
            foreach (Match range in RangeTriple().Matches(block.Groups[1].Value))
            {
                var from = Convert.ToInt32(range.Groups[1].Value, 16);
                var to = Convert.ToInt32(range.Groups[2].Value, 16);
                var destination = Convert.ToInt32(range.Groups[3].Value, 16);
                for (var code = from; code <= to; code++) map[code] = char.ConvertFromUtf32(destination + (code - from));
            }
        }

        return map;
    }

    private static string FromUtf16Hex(string hex)
    {
        var builder = new StringBuilder();
        for (var i = 0; i + 3 < hex.Length; i += 4) builder.Append((char)Convert.ToInt32(hex.Substring(i, 4), 16));
        return builder.ToString();
    }

    [GeneratedRegex(@"(\d+)\s+\d+\s+obj")] private static partial Regex ObjectStart();
    [GeneratedRegex(@"/ToUnicode\s+(\d+)\s+\d+\s+R")] private static partial Regex ToUnicodeRef();
    [GeneratedRegex(@"/(F\d+)\s+(\d+)\s+\d+\s+R")] private static partial Regex FontResource();
    [GeneratedRegex(@"/(?<font>F\d+)\s+[\d.]+\s+Tf|<(?<hex>[0-9A-Fa-f]+)>\s*Tj|\[(?<array>[^\]]*)\]\s*TJ|(?<newline>\bT\*|\bET\b)")] private static partial Regex ContentToken();
    [GeneratedRegex(@"<([0-9A-Fa-f]+)>")] private static partial Regex HexString();
    [GeneratedRegex(@"beginbfchar(.*?)endbfchar", RegexOptions.Singleline)] private static partial Regex BfChar();
    [GeneratedRegex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>")] private static partial Regex CharPair();
    [GeneratedRegex(@"beginbfrange(.*?)endbfrange", RegexOptions.Singleline)] private static partial Regex BfRange();
    [GeneratedRegex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>")] private static partial Regex RangeTriple();
}
