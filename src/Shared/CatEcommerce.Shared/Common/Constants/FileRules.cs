using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-FIL-001 §8.3 -- the allow-list per <see cref="FilePurpose"/>: declared content type
/// (checked twice: against this list, and against the leading bytes actually on disk) and the
/// maximum size. Domain configuration data owned by this context, not a seam another area calls
/// through (docs/handover/CONTRACTS.md §5) -- a new purpose is a new <see cref="FilePurpose"/>
/// member plus a row here, in the same commit, per §8.3's own rule.
///
/// <see cref="FilePurpose.CatProfilePhoto"/>'s row was added by HO-FIL-001 itself, under
/// integrator authorisation, to close the carried HO-CAT-002-SECURITY item (see HO-FIL-001 §1
/// Amended) -- its cap and allowed types match the pre-existing <c>CatPhotoRules</c> the Cat
/// Profiles aggregate already enforced, so the aggregate-level check and the upload-time check
/// agree.
/// </summary>
public static class FileRules
{
    public const long OneMb = 1024L * 1024L;

    /// <summary>
    /// How long a minted <see cref="FilePurpose"/> file reference stays eligible to be attached
    /// by a consuming write (e.g. Cat Profiles' "add photo"). Not named by any handover verbatim
    /// -- inferred so a leaked/old file id cannot be replayed indefinitely by a consumer that
    /// checks freshness (the "expiry check" called for by the carried HO-CAT-002-SECURITY item).
    /// A FIL-owned policy, not a business rule of any consuming area.
    /// </summary>
    public static readonly TimeSpan AttachWindow = TimeSpan.FromMinutes(30);

    private static readonly IReadOnlyDictionary<FilePurpose, FilePurposeRule> Rules =
        new Dictionary<FilePurpose, FilePurposeRule>
        {
            [FilePurpose.LabResult] = new(
                FilePurpose.LabResult,
                ["application/pdf", "image/jpeg", "image/png", "application/dicom"],
                50 * OneMb),

            [FilePurpose.ClinicalPhoto] = new(
                FilePurpose.ClinicalPhoto,
                ["image/jpeg", "image/png", "image/webp"],
                15 * OneMb),

            [FilePurpose.ConsentForm] = new(
                FilePurpose.ConsentForm,
                ["application/pdf", "image/jpeg", "image/png"],
                15 * OneMb),

            [FilePurpose.ProductImage] = new(
                FilePurpose.ProductImage,
                ["image/jpeg", "image/png", "image/webp"],
                5 * OneMb),

            [FilePurpose.BoardingMedia] = new(
                FilePurpose.BoardingMedia,
                ["image/jpeg", "image/png", "image/webp", "video/mp4"],
                50 * OneMb),

            [FilePurpose.BoardingBelongings] = new(
                FilePurpose.BoardingBelongings,
                ["image/jpeg", "image/png", "image/webp"],
                15 * OneMb),

            [FilePurpose.ReportExport] = new(
                FilePurpose.ReportExport,
                ["application/pdf", "text/csv", "application/zip"],
                50 * OneMb),

            // Integrator-authorised addition -- see the type doc comment above.
            [FilePurpose.CatProfilePhoto] = new(
                FilePurpose.CatProfilePhoto,
                ["image/jpeg", "image/png", "image/webp"],
                CatPhotoRules.MaxSizeInBytes),
        };

    public static FilePurposeRule? Find(FilePurpose purpose) =>
        Rules.TryGetValue(purpose, out var rule) ? rule : null;

    /// <summary>
    /// §8.2 step 4: "Leading bytes agree with the declared type". One checker per content type,
    /// shared across every purpose that allows it. <c>text/csv</c> has no fixed file signature by
    /// definition (RFC 4180) -- the closest honest check is "this looks like text, not an
    /// arbitrary binary blob wearing a csv label", so it is checked instead for the absence of a
    /// NUL byte and any control character other than tab/CR/LF in the sampled header. The
    /// markup/script deny-list (<see cref="DeniedTextSignatures"/>) is a SEPARATE, whole-file check
    /// -- see <see cref="ContainsDeniedCsvSignature"/> -- not part of this header-only sample, since
    /// a 256-byte sample only ever caught a payload placed at the very start of the file (gate-fix
    /// 2026-09-27, SECURITY LOW second pass). Flagged here because it is FIL's own inference, not a
    /// verbatim rule from any handover.
    /// </summary>
    public static bool MagicBytesMatch(string contentType, ReadOnlySpan<byte> header) => contentType switch
    {
        "application/pdf" => StartsWith(header, "%PDF-"u8),
        "image/jpeg" => StartsWith(header, [0xFF, 0xD8, 0xFF]),
        "image/png" => StartsWith(header, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        "application/dicom" => header.Length >= 132 && header.Slice(128, 4).SequenceEqual("DICM"u8),
        "image/webp" => header.Length >= 12
                         && header[..4].SequenceEqual("RIFF"u8)
                         && header.Slice(8, 4).SequenceEqual("WEBP"u8),
        "video/mp4" => header.Length >= 8 && header.Slice(4, 4).SequenceEqual("ftyp"u8),
        "application/zip" => StartsWith(header, [0x50, 0x4B, 0x03, 0x04]),
        "text/csv" => LooksLikeCsv(header),
        _ => false
    };

    private static bool StartsWith(ReadOnlySpan<byte> header, ReadOnlySpan<byte> prefix) =>
        header.Length >= prefix.Length && header[..prefix.Length].SequenceEqual(prefix);

    /// <summary>Case-insensitive markup/script signatures that must never be accepted as CSV, even
    /// though they contain no NUL or disallowed control bytes and so would otherwise pass the
    /// plain "looks like text" check.</summary>
    private static readonly string[] DeniedTextSignatures =
    [
        "<!doctype", "<html", "<head", "<body", "<script", "<?php", "<%", "<hta:", "#!/bin/",
    ];

    private static bool LooksLikeCsv(ReadOnlySpan<byte> header)
    {
        if (header.Length == 0) return false;

        foreach (var b in header)
        {
            if (b == 0) return false;
            var isControlOutsideWhitespace = b < 0x20 && b is not (0x09 or 0x0A or 0x0D);
            if (isControlOutsideWhitespace) return false;
        }

        return true;
    }

    /// <summary>
    /// Gate-fix 2026-09-27 (SECURITY LOW, second pass): scans the WHOLE stream for
    /// <see cref="DeniedTextSignatures"/> with a rolling window (mirrors
    /// <c>EicarTestScanner</c>'s own technique), not just the 256-byte magic-byte sample --
    /// previously a benign CSV prefix followed by an HTA/script payload later in the file was
    /// accepted (probed and confirmed: 360 bytes of CSV then <c>&lt;HTA:APPLICATION/&gt;</c> ->
    /// 201). Only called for <c>text/csv</c> uploads. Case-insensitive; every signature is pure
    /// ASCII, so a byte-for-byte case-folding search is exact (no encoding ambiguity to exploit).
    /// </summary>
    public static async Task<bool> ContainsDeniedCsvSignatureAsync(Stream content, CancellationToken ct)
    {
        var longestSignature = DeniedTextSignatures.Max(s => s.Length);
        var overlapLength = longestSignature - 1;
        const int chunkSize = 81920;
        var window = new byte[overlapLength + chunkSize];
        var windowFilled = 0;

        var buffer = new byte[chunkSize];
        int read;
        while ((read = await content.ReadAsync(buffer, ct)) > 0)
        {
            Buffer.BlockCopy(buffer, 0, window, windowFilled, read);
            windowFilled += read;

            var text = System.Text.Encoding.ASCII.GetString(window, 0, windowFilled);
            foreach (var signature in DeniedTextSignatures)
            {
                if (text.Contains(signature, StringComparison.OrdinalIgnoreCase)) return true;
            }

            var keep = Math.Min(overlapLength, windowFilled);
            Buffer.BlockCopy(window, windowFilled - keep, window, 0, keep);
            windowFilled = keep;
        }

        return false;
    }
}

/// <summary>One <see cref="FileRules"/> row (HO-FIL-001 §8.3).</summary>
public sealed record FilePurposeRule(FilePurpose Purpose, IReadOnlyList<string> AllowedContentTypes, long MaxSizeInBytes);
