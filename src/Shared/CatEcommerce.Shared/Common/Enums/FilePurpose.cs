namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// Owned by HO-FIL-001 §8.3/§10.1 (ADR-318). What an uploaded file is for -- the only thing this
/// bounded context knows about a file. §10.1: "Appended by the consuming handover, with a
/// <c>FileRules</c> row in the same commit." The seven members below are every purpose already
/// named across the written handovers (HO-EMR-001, HO-SHP-001, HO-BRD-001, HO-RPT-001) at the time
/// of this contracts pass; a later area introducing a new purpose still appends here plus its own
/// <c>FileRules</c> row (owned by HO-FIL-001, not built in this pass).
///
/// <see cref="CatProfilePhoto"/> was added by HO-FIL-001 itself (not the usual "consuming handover
/// appends its own member" path) under integrator authorisation, to close the carried security item
/// from HO-CAT-002-SECURITY (the cat photo endpoint trusted a client-supplied storage key). See
/// HO-FIL-001 §1 Amended.
/// </summary>
public sealed class FilePurpose(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly FilePurpose LabResult = new(1, nameof(LabResult));
    public static readonly FilePurpose ClinicalPhoto = new(2, nameof(ClinicalPhoto));
    public static readonly FilePurpose ConsentForm = new(3, nameof(ConsentForm));
    public static readonly FilePurpose ProductImage = new(4, nameof(ProductImage));
    public static readonly FilePurpose BoardingMedia = new(5, nameof(BoardingMedia));
    public static readonly FilePurpose BoardingBelongings = new(6, nameof(BoardingBelongings));
    public static readonly FilePurpose ReportExport = new(7, nameof(ReportExport));
    public static readonly FilePurpose CatProfilePhoto = new(8, nameof(CatProfilePhoto));
}
