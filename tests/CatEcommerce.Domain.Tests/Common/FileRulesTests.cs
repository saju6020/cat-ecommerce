using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-FIL-001 §16 U1-U3.</summary>
public sealed class FileRulesTests
{
    public static IEnumerable<object[]> EveryCanonicalPurpose()
    {
        yield return [FilePurpose.LabResult];
        yield return [FilePurpose.ClinicalPhoto];
        yield return [FilePurpose.ConsentForm];
        yield return [FilePurpose.ProductImage];
        yield return [FilePurpose.BoardingMedia];
        yield return [FilePurpose.BoardingBelongings];
        yield return [FilePurpose.ReportExport];
        yield return [FilePurpose.CatProfilePhoto];
    }

    // U1
    [Theory]
    [MemberData(nameof(EveryCanonicalPurpose))]
    public void Every_purpose_has_a_rule_and_every_declared_type_in_it_agrees_with_its_own_magic_bytes(FilePurpose purpose)
    {
        var rule = FileRules.Find(purpose);
        Assert.NotNull(rule);
        Assert.NotEmpty(rule!.AllowedContentTypes);

        foreach (var contentType in rule.AllowedContentTypes)
        {
            var header = SampleHeaderFor(contentType);
            Assert.True(FileRules.MagicBytesMatch(contentType, header), $"{contentType} should match its own sample header.");
        }
    }

    // U2
    [Fact]
    public void Pdf_bytes_declared_as_png_do_not_match()
    {
        var pdfHeader = "%PDF-1.4"u8.ToArray();
        Assert.False(FileRules.MagicBytesMatch("image/png", pdfHeader));
    }

    [Fact]
    public void Png_bytes_declared_as_pdf_do_not_match()
    {
        var pngHeader = SampleHeaderFor("image/png");
        Assert.False(FileRules.MagicBytesMatch("application/pdf", pngHeader));
    }

    // U3
    [Theory]
    [MemberData(nameof(EveryCanonicalPurpose))]
    public void Svg_octet_stream_and_exe_are_never_allowed_for_any_purpose(FilePurpose purpose)
    {
        var rule = FileRules.Find(purpose)!;

        Assert.DoesNotContain("image/svg+xml", rule.AllowedContentTypes, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("application/octet-stream", rule.AllowedContentTypes, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("application/x-msdownload", rule.AllowedContentTypes, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("*/*", rule.AllowedContentTypes, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Exe_bytes_never_match_any_allowed_content_type()
    {
        var exeHeader = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }; // "MZ" DOS header

        foreach (var contentType in new[] { "application/pdf", "image/jpeg", "image/png", "image/webp", "application/dicom", "video/mp4", "application/zip" })
        {
            Assert.False(FileRules.MagicBytesMatch(contentType, exeHeader), $"MZ header should never satisfy {contentType}.");
        }
    }

    private static byte[] SampleHeaderFor(string contentType) => contentType switch
    {
        "application/pdf" => "%PDF-1.4\n%%EOF"u8.ToArray(),
        "image/jpeg" => [0xFF, 0xD8, 0xFF, 0xE0],
        "image/png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
        "application/dicom" => BuildDicomHeader(),
        "image/webp" => BuildRiffWebpHeader(),
        "video/mp4" => [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70],
        "application/zip" => [0x50, 0x4B, 0x03, 0x04],
        "text/csv" => "name,age\nWhiskers,3\n"u8.ToArray(),
        _ => throw new NotSupportedException(contentType)
    };

    private static byte[] BuildDicomHeader()
    {
        var header = new byte[132];
        "DICM"u8.CopyTo(header.AsSpan(128));
        return header;
    }

    private static byte[] BuildRiffWebpHeader()
    {
        var header = new byte[12];
        "RIFF"u8.CopyTo(header.AsSpan(0));
        "WEBP"u8.CopyTo(header.AsSpan(8));
        return header;
    }

    // Gate-fix 2026-09-27 (SECURITY LOW, second pass): ContainsDeniedCsvSignatureAsync scans the
    // WHOLE stream, not just a 256-byte sample -- this is the exact scenario the SECURITY gate
    // probed and found exploitable against the old header-only check.
    [Fact]
    public async Task ContainsDeniedCsvSignatureAsync_finds_an_hta_payload_after_360_bytes_of_benign_csv()
    {
        var benignPrefix = string.Concat(Enumerable.Repeat("name,age\nWhiskers,3\n", 18)); // > 360 bytes
        var content = System.Text.Encoding.ASCII.GetBytes(benignPrefix + "<HTA:APPLICATION/><script language=vbscript>msgbox 1</script>");

        var result = await FileRules.ContainsDeniedCsvSignatureAsync(new MemoryStream(content), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ContainsDeniedCsvSignatureAsync_is_false_for_ordinary_csv()
    {
        var content = "name,age\nWhiskers,3\nMittens,5\n"u8.ToArray();

        var result = await FileRules.ContainsDeniedCsvSignatureAsync(new MemoryStream(content), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ContainsDeniedCsvSignatureAsync_finds_a_signature_split_across_a_chunk_boundary()
    {
        // 81920 is the scanner's own chunk size -- place the signature straddling that boundary.
        var leading = new string('a', 81920 - 5);
        var content = System.Text.Encoding.ASCII.GetBytes(leading + "<script>alert(1)</script>");

        var result = await FileRules.ContainsDeniedCsvSignatureAsync(new MemoryStream(content), CancellationToken.None);

        Assert.True(result);
    }
}
