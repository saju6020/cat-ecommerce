namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-FIL-001 §8.6: "inline for JPEG, PNG, WebP, MP4 and PDF when requested, always attachment
/// for DICOM and ZIP". Used twice -- once by <c>GetStoredFileQueryHandler</c> to decide what
/// disposition to sign into the URL, and again by <c>FilesController</c>'s serving endpoint to
/// decide the actual <c>Content-Disposition</c> header -- so a DICOM/ZIP row is always served as
/// an attachment even if a caller's URL somehow carries <c>disp=inline</c> (I7).
/// </summary>
public static class FileServingRules
{
    public const string Inline = "inline";
    public const string Attachment = "attachment";

    private static readonly HashSet<string> InlineEligibleContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp", "video/mp4", "application/pdf" };

    private static readonly HashSet<string> AlwaysAttachmentContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "application/dicom", "application/zip" };

    public static string ResolveDisposition(string contentType, string requestedDisposition)
    {
        if (AlwaysAttachmentContentTypes.Contains(contentType)) return Attachment;

        return string.Equals(requestedDisposition, Inline, StringComparison.OrdinalIgnoreCase)
               && InlineEligibleContentTypes.Contains(contentType)
            ? Inline
            : Attachment;
    }
}
