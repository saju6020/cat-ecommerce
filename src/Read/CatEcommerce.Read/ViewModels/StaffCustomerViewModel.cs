namespace CatEcommerce.Read.ViewModels;

/// <summary>
/// HO-ACC-002 CR MEDIUM 2 (first-pass gate fix round, item 13): <paramref name="Cats"/> is new --
/// this fresh single-customer read (Q-H1, the one <c>MergeReview</c> uses, ADR-FE-011) used to
/// report only <paramref name="CatCount"/>, unlike the duplicates report (Q-I1,
/// <see cref="DuplicateCustomerGroupViewModel"/>), which already lists cats by name. Reuses
/// <see cref="DuplicateCustomerCatViewModel"/> rather than a second, identically-shaped type --
/// despite its name, the shape (cat id/name/deceased flag) is not duplicates-specific.
/// <paramref name="CatCount"/> is kept, unchanged, for any other consumer already reading it
/// (<c>CustomerDetail.tsx</c>) -- always equal to <c>Cats.Count</c>.
/// </summary>
public sealed record StaffCustomerViewModel(
    Guid Id, string FullName, string? MobileNumber, string? EmailAddress,
    string RecordStatus, string RegistrationChannel, int CatCount, string Status, Guid? MergedIntoCustomerId,
    IReadOnlyList<DuplicateCustomerCatViewModel> Cats);

/// <summary>
/// <paramref name="MobileMatchedOn"/> is <c>"Primary"</c>, <c>"Additional"</c> or <c>null</c>
/// (matched by email, or no mobile search term was given) -- SECURITY gate MEDIUM: additional
/// numbers are never verified, so the screen must be able to show that a hit is only an unverified
/// claim on someone else's number, not treat it the same as a genuine primary-number match.
/// </summary>
public sealed record StaffCustomerSummaryViewModel(
    Guid Id, string FullName, string? MobileNumber, string? EmailAddress, string RecordStatus, string Status,
    string? MobileMatchedOn);
