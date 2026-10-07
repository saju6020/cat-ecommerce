using System.Globalization;
using System.Text;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-FIL-001 §8.1/U5: strips any path component and control characters (including NUL), then
/// bounds the length to 255. The sanitised name is cosmetic only -- it never affects
/// <c>StorageKeyFactory</c>'s output, which never reads it (U5/U6).
///
/// Gate-fix 2026-09-27 (SECURITY MEDIUM, HO-FIL-001-SECURITY.md): <see cref="Sanitize"/> now also
/// strips Unicode category Cf (format -- bidi overrides such as U+202E RTLO, zero-width
/// characters) in addition to Cc (control), closing the <c>invoice&#x202E;gnp.hta</c> ("displays as
/// invoice...png") trick at the point the name is first stored, not only when it happens to be
/// served. <see cref="ForDownload"/> additionally replaces whatever extension the stem carries with
/// the canonical one for the file's verified content type, so a served name can never claim to be a
/// type the bytes are not (e.g. an HTA saved and served as <c>Quarterly-report.hta</c> because the
/// declared content type happened to be <c>text/csv</c>).
/// </summary>
public static class FileNameSanitizer
{
    public const int MaxLength = 255;

    private static readonly IReadOnlyDictionary<string, string> CanonicalExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["application/pdf"] = ".pdf",
            ["video/mp4"] = ".mp4",
            ["application/dicom"] = ".dcm",
            ["application/zip"] = ".zip",
            ["text/csv"] = ".csv",
        };

    public static string Sanitize(string? rawFileName)
    {
        var name = string.IsNullOrWhiteSpace(rawFileName) ? "upload" : rawFileName.Trim('"');
        name = name.Replace('\\', '/');

        var lastSlash = name.LastIndexOf('/');
        if (lastSlash >= 0) name = name[(lastSlash + 1)..];

        var builder = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format) continue;
            builder.Append(ch);
        }

        var sanitized = builder.ToString().Trim();
        if (sanitized.Length == 0) sanitized = "upload";
        return sanitized.Length > MaxLength ? sanitized[..MaxLength] : sanitized;
    }

    /// <summary>Gate-fix 2026-09-27: the name actually served in <c>Content-Disposition</c> --
    /// <paramref name="storedFileName"/>'s own extension is discarded entirely (it is already
    /// sanitised by <see cref="Sanitize"/> at upload time, but a caller must not be able to make a
    /// served name end in <c>.hta</c>/<c>.bat</c>/<c>.ps1</c>/anything else executable purely by
    /// naming the upload that when it was stored) and replaced with the canonical extension for
    /// <paramref name="contentType"/> -- the verified, stored content type, never a client-declared
    /// one at serve time.</summary>
    public static string ForDownload(string storedFileName, string contentType)
    {
        var stem = System.IO.Path.GetFileNameWithoutExtension(storedFileName);
        if (string.IsNullOrWhiteSpace(stem)) stem = "file";

        var extension = CanonicalExtensions.GetValueOrDefault(contentType, string.Empty);
        return stem + extension;
    }
}
