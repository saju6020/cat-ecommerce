using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-FIL-001 §9 OQ-7: "Who may call the upload endpoint for each purpose" -- "<c>ActionRoles</c>
/// per purpose, named by the consuming handover; the endpoint refuses a purpose the caller has no
/// action for."
///
/// Gate-fix 2026-09-27 (SECURITY MEDIUM, HO-FIL-001-SECURITY.md): fails CLOSED. A purpose with no
/// entry in <see cref="StaffPurposeActions"/> is refused for every staff role -- the earlier
/// "unmapped means Files.Upload alone is enough" reading of OQ-7 let any of the eight staff roles
/// upload under any purpose, including clinical and report purposes no consuming area had actually
/// claimed yet (probed and confirmed exploitable by the SECURITY gate). <c>[RequireAction
/// (Actions.Files.Upload)]</c> still proves the caller is authenticated staff; this registry is the
/// *only* thing that decides which purposes that caller may actually use. Only
/// <see cref="FilePurpose.CatProfilePhoto"/> is mapped so far, to the same
/// <c>Actions.Cats.EditProfile</c> action the endpoint it replaced already required (integrator
/// authorisation, HO-FIL-001 §1 Amended). A future purpose that genuinely needs to stay open to
/// every staff role (e.g. a purpose with no sensitive content at all) still names that explicitly
/// here rather than relying on an absent entry to mean "allowed".
///
/// A test-only purpose (e.g. <c>ProductImage</c> for HO-FIL-001 §16's own I1, since HO-SHP-001 has
/// not landed) is registered by the test host's own <c>IFilePurposeAccessPolicy</c> decorator
/// (<c>FilesApiFactory</c>), never here -- production code is not widened for a test fixture's
/// convenience.
///
/// A plain dictionary rather than the <c>ActionRoles</c>/<c>SettingDefinitions</c>
/// reflection-aggregated partial pattern (ADR-341): with a single purpose mapped so far, the
/// collision risk that pattern guards against does not yet exist. A second area registering its
/// own purpose here only needs to add one dictionary entry; if contention between areas' entries
/// becomes a real problem later, upgrading to the same per-area-partial-plus-reflection shape as
/// <c>ActionRoles</c> is a mechanical change, not a redesign.
///
/// Customers may upload only <see cref="FilePurpose.CatProfilePhoto"/> (gate-fix 2026-09-27,
/// SECURITY MEDIUM note: "customers hold Cats.EditProfile ... but no customer purpose exists" --
/// customers had lost the ability to add cat photos entirely). Which specific cat a customer may
/// attach a photo to is decided later, at attach time, by <c>OwnerScope.Cat</c> on
/// <c>POST /api/cats/{catId}/photos</c> -- upload itself is cat-agnostic.
/// </summary>
public static class FilePurposeAccess
{
    private static readonly IReadOnlyDictionary<FilePurpose, string> StaffPurposeActions =
        new Dictionary<FilePurpose, string>
        {
            // Integrator-authorised: closes the HO-CAT-002-SECURITY carried item. The action
            // matches the one that already governed the old, client-trusting endpoint.
            [FilePurpose.CatProfilePhoto] = Actions.Cats.EditProfile,

            // HO-SHP-001 (2026-10-04, found while writing U-A5b): the Shop catalogue consumes
            // ProductImage files (SetProductImagesCommandHandler) but no mapping existed, so the
            // fail-closed policy refused every staff role -- a product image could not be uploaded
            // in the real host. Same action that governs the product write that attaches it.
            [FilePurpose.ProductImage] = Actions.Shop.ProductsManage,

            // HO-EMR-001 (2026-10-07): the three clinical purposes HO-FIL-001 pre-declared. A lab file or a
            // clinical photo is uploaded by the vet who will attach it; a signed consent form by whoever
            // captures the consent (Reception, Nurse, Vet, Administrator). Customers upload none of them.
            [FilePurpose.LabResult] = Actions.Visits.WriteNote,
            [FilePurpose.ClinicalPhoto] = Actions.Visits.WriteNote,
            [FilePurpose.ConsentForm] = Actions.Consents.Record,

            // HO-BRD-001 (2026-10-14, found writing I-F7): the two boarding purposes HO-FIL-001 pre-declared had no mapping, so the fail-closed policy refused every staff role.
            // A diary photo is uploaded by whoever records care; a belongings photo by whoever checks the cat in.
            [FilePurpose.BoardingMedia] = Actions.Boarding.RecordCare,
            [FilePurpose.BoardingBelongings] = Actions.Boarding.CheckInOut,
        };

    private static readonly IReadOnlySet<FilePurpose> CustomerPurposes = new HashSet<FilePurpose>
    {
        FilePurpose.CatProfilePhoto,
    };

    public static bool TryGetRequiredStaffAction(FilePurpose purpose, out string action)
    {
        if (StaffPurposeActions.TryGetValue(purpose, out var found))
        {
            action = found;
            return true;
        }

        action = "";
        return false;
    }

    public static bool IsAllowedForCustomer(FilePurpose purpose) => CustomerPurposes.Contains(purpose);
}
