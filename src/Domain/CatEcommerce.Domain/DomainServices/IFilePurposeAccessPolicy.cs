using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Gate-fix 2026-09-27 (SECURITY MEDIUM, HO-FIL-001-SECURITY.md): an injectable seam around the
/// otherwise-static <c>FilePurposeAccess</c> registry, so a test host can register an additional,
/// test-only purpose mapping (e.g. <c>ProductImage</c>, owned by HO-SHP-001 and not yet landed, for
/// HO-FIL-001 §16's own I1) without editing production code or widening what a real deployment
/// accepts. <c>CatEcommerce.Infrastructure.Files.DefaultFilePurposeAccessPolicy</c> is the
/// production implementation.
/// </summary>
public interface IFilePurposeAccessPolicy
{
    /// <summary>Fails closed: <c>false</c> for any purpose with no registered mapping.</summary>
    bool TryGetRequiredStaffAction(FilePurpose purpose, out string action);

    bool IsAllowedForCustomer(FilePurpose purpose);
}
