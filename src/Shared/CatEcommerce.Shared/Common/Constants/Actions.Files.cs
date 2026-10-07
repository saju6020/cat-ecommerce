namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// File Storage (HO-FIL-001) action names. <see cref="Files.Upload"/> is deliberately coarse --
/// "any signed-in staff member may call the upload endpoint at all" -- because the finer-grained
/// "which purposes may this caller upload for" decision (OQ-7) varies per <c>FilePurpose</c> and
/// cannot be expressed as a single compile-time <c>[RequireAction]</c> constant on one route.
/// <see cref="FilePurposeAccess"/> makes that second, purpose-scoped decision inside the
/// controller action itself, after this attribute-level check passes. See ADR-341 for the
/// per-area partial pattern.
/// </summary>
public static partial class Actions
{
    public static class Files
    {
        public const string Upload = "Files.Upload";
    }
}
