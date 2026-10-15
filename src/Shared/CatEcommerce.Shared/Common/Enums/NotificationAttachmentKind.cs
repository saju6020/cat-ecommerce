namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-RPT-001 section 10.1 / 11.7 (HO-NTF-001 amendment) -- what an attachment by reference points at. Append only.</summary>
public sealed class NotificationAttachmentKind(int value, string displayName) : Enumeration(value, displayName)
{
    /// <summary>A scheduled report run: the id is the run's, its file lives in HO-FIL-001.</summary>
    public static readonly NotificationAttachmentKind ReportRun = new(1, nameof(ReportRun));
}
