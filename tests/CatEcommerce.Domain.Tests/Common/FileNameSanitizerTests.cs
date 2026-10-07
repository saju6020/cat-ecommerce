using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>U5.</summary>
public sealed class FileNameSanitizerTests
{
    [Fact]
    public void Strips_path_traversal_and_the_null_byte()
    {
        var sanitized = FileNameSanitizer.Sanitize("../../etc/passwd\u0000.png");

        Assert.Equal("passwd.png", sanitized);
        Assert.DoesNotContain("..", sanitized);
        Assert.DoesNotContain('/', sanitized);
        Assert.DoesNotContain('\u0000', sanitized);
    }

    [Fact]
    public void Strips_windows_style_path_traversal()
    {
        var sanitized = FileNameSanitizer.Sanitize(@"..\..\windows\system32\evil.exe");

        Assert.Equal("evil.exe", sanitized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Falls_back_to_a_default_name_when_nothing_usable_remains(string? raw)
    {
        Assert.Equal("upload", FileNameSanitizer.Sanitize(raw));
    }

    [Fact]
    public void Bounds_the_length_to_255()
    {
        var sanitized = FileNameSanitizer.Sanitize(new string('a', 500) + ".png");

        Assert.True(sanitized.Length <= FileNameSanitizer.MaxLength);
    }

    [Fact]
    public void Ordinary_names_pass_through_unchanged()
    {
        Assert.Equal("cat-photo.jpg", FileNameSanitizer.Sanitize("cat-photo.jpg"));
    }

    // Gate-fix 2026-09-27 (SECURITY MEDIUM, HO-FIL-001-SECURITY.md): Sanitize strips Unicode
    // category Cf (format -- bidi overrides, zero-width characters), not just Cc (control). Only
    // manually probed by the SECURITY gate before this test existed (TEST gate).
    [Fact]
    public void Strips_the_RTLO_bidi_override_character()
    {
        var sanitized = FileNameSanitizer.Sanitize("invoice‮gnp.hta");

        Assert.Equal("invoicegnp.hta", sanitized);
        Assert.DoesNotContain('‮', sanitized);
    }

    [Fact]
    public void Strips_zero_width_space_and_LTR_embedding()
    {
        var sanitized = FileNameSanitizer.Sanitize("a​b‪c.png");

        Assert.Equal("abc.png", sanitized);
    }

    [Fact]
    public void A_name_made_only_of_format_characters_falls_back_to_the_default()
    {
        var sanitized = FileNameSanitizer.Sanitize("‮‪​");

        Assert.Equal("upload", sanitized);
    }

    // Gate-fix 2026-09-27 (SECURITY MEDIUM, second pass): ForDownload discards whatever extension
    // the stored name carries and substitutes the canonical one for the verified, stored content
    // type -- SECURITY's own probe table (HO-FIL-001-SECURITY.md second pass), never persisted as
    // a unit test until this TEST gate.
    [Theory]
    [InlineData("cat.hta", "image/png", "cat.png")]
    [InlineData("a.exe", "application/zip", "a.zip")]
    [InlineData("scan.js", "application/dicom", "scan.dcm")]
    [InlineData("v.scr", "video/mp4", "v.mp4")]
    [InlineData("a.html", "application/pdf", "a.pdf")]
    [InlineData("Quarterly-report.hta", "text/csv", "Quarterly-report.csv")]
    [InlineData("photo.png.exe", "image/png", "photo.png.png")]
    [InlineData("evil.hta.", "image/png", "evil.hta.png")]
    [InlineData(".hta", "image/png", "file.png")]
    public void ForDownload_forces_the_canonical_extension_for_the_stored_content_type(
        string storedFileName, string contentType, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.ForDownload(storedFileName, contentType));
    }

    [Fact]
    public void ForDownload_with_an_unrecognised_content_type_keeps_only_the_stem()
    {
        Assert.Equal("a", FileNameSanitizer.ForDownload("a.png", "application/unknown"));
    }

    // The extension-forcing must survive a bidi character the name was stored with before
    // Sanitize's own Cf stripping existed -- ForDownload never re-runs Sanitize, so this proves
    // GetFileNameWithoutExtension alone does not reintroduce it into the served name.
    [Fact]
    public void ForDownload_never_reintroduces_a_bidi_character_from_the_stem()
    {
        var stored = FileNameSanitizer.Sanitize("invoice‮gnp.hta");
        var served = FileNameSanitizer.ForDownload(stored, "image/png");

        Assert.DoesNotContain('‮', served);
        Assert.Equal("invoicegnp.png", served);
    }
}
