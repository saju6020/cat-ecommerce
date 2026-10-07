using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>File Storage's own three settings (HO-FIL-001 §10.3), added under ADR-341's
/// per-area-partial pattern. Gate-fix 2026-09-27 (ARCHITECTURE MEDIUM): these were previously read
/// from <c>FileStorageOptions</c> (plain configuration) instead of the ADM settings registry --
/// bucket name, credentials, scanner host and the signing key stay configuration (§10.3: "they are
/// not an administrator's to change at runtime"); these three numbers are.</summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> FilesEntries =
    [
        // Read-only display of FileRules' own per-purpose caps (the largest is 50 MB) -- not
        // itself enforced (FileRules is domain configuration data, not administrator-editable),
        // but §10.3 lists it as a registry entry so an admin settings screen can show it.
        new SettingDefinition(
            SettingKeys.FilesMaxUploadMb, "Files", SettingType.Integer, "1", "100", "MB", "50",
            false, "Maximum upload size", "The largest per-purpose upload cap currently in force (display only; the real caps are FileRules, not administrator-editable)."),

        new SettingDefinition(
            SettingKeys.FilesSignedUrlMinutes, "Files", SettingType.Integer, "1", "60", "minutes", "5",
            false, "Signed URL lifetime", "How long a signed file-serving URL (§8.6) remains valid after it is issued."),

        new SettingDefinition(
            SettingKeys.FilesScanTimeoutSeconds, "Files", SettingType.Integer, "5", "120", "seconds", "30",
            false, "Malware scan timeout", "How long the upload pipeline waits for ClamAV before treating the scan as Unavailable (ADR-319, fail closed)."),
    ];
}
