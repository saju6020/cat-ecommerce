namespace CatEcommerce.Read.ViewModels;

// HO-EMR-001 §8.9 / §12.4 -- consent. The signature's coordinates are never returned as JSON: they are served only as an SVG made
// of polylines (GET .../signature.svg), so no client ever renders stored data as anything but numbers drawn as lines.

/// <summary><c>GET /api/staff/consent-forms/{kind}</c> -- the current published wording (HO-ADM-001 policy document), as
/// <c>LimitedMarkup</c> (never HTML) with its version number, which the consent stores.</summary>
public sealed record ConsentFormViewModel(string PolicyKey, int VersionNumber, string Title, string Body);

public sealed record ConsentViewModel(
    Guid Id, int Version, Guid CatId, Guid AppointmentId, string Kind, string ProcedureDescription, string Method,
    string SignedByName, string SignerRelationship, Guid? SignedByCustomerId, DateTimeOffset SignedOn,
    string PolicyKey, int PolicyVersionNumber, string CapturedBy, string? Witness,
    string Status, DateTimeOffset? WithdrawnOn, string? WithdrawnBy, string? WithdrawalReason,
    string BranchTimeZoneId,
    string? SignatureUrl,
    StoredFileViewModel? File);

public sealed record ConsentListItemViewModel(
    Guid Id, Guid AppointmentId, string Kind, string Method, string Status, DateTimeOffset SignedOn, int PolicyVersionNumber, string SignedByName);

/// <summary><c>GET /api/staff/consents/status?catId&amp;appointmentId&amp;kind</c> -- the HO-SRG-001 contract: is there a valid
/// consent for this cat, this procedure booking and this kind (US-CLN-21 AC2)?</summary>
public sealed record ConsentStatusViewModel(bool HasValidConsent, Guid? ConsentId, DateTimeOffset? SignedOn);
