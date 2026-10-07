namespace CatEcommerce.Shared.Common.Constants;

/// <summary>File Storage's (HO-FIL-001) own registry keys, §10.3. Split out of the shared
/// <c>SettingKeys.cs</c> (gate-fix 2026-09-27, ARCHITECTURE MEDIUM second pass) so this area never
/// needs to edit a file another area also edits -- ADR-341's per-area partial pattern.</summary>
public static partial class SettingKeys
{
    public const string FilesMaxUploadMb = "files.maxUploadMb";
    public const string FilesSignedUrlMinutes = "files.signedUrlMinutes";
    public const string FilesScanTimeoutSeconds = "files.scanTimeoutSeconds";
}
