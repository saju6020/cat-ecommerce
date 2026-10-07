namespace CatEcommerce.Read.ViewModels;

public sealed record DuplicateCustomerCatViewModel(Guid CatId, string Name, bool IsDeceased);

/// <summary>
/// SECURITY gate LOW (second pass, HO-ACC-001 §1 Amended (18)): <see cref="MobileMatchedOn"/> is
/// "Primary" when this member's own PRIMARY mobile is one of the numbers shared with another
/// member of the group, "Additional" when the only phone overlap is via one of this member's
/// unverified additional numbers, and <c>null</c> when the group's overlap is by email only. An
/// "Additional" label means staff should confirm identity before merging or attaching cats from
/// this pairing -- an additional number is never verified, so it can be someone else's real
/// primary mobile that this record's owner merely typed in (see <c>FindCustomersQueryHandler</c>'s
/// identical Q-H2 label).
///
/// <paramref name="RecordStatus"/>/<paramref name="RegistrationChannel"/>: HO-ACC-002 CR MEDIUM 2
/// (first-pass gate fix round, item 13) -- §11.12's prose already asked for these per member; this
/// view model didn't carry them, a §20.7 contract gap this round closes rather than continuing to
/// document as unmet. Same values/derivation as <see cref="StaffCustomerViewModel"/>'s own fields
/// (<c>CustomerRecordStatusRules.Derive</c>, the aggregate's own <c>RegistrationChannel</c>).
/// </summary>
public sealed record DuplicateCustomerMemberViewModel(
    Guid CustomerId, string FullName, string? MobileNumber, string? EmailAddress,
    IReadOnlyList<DuplicateCustomerCatViewModel> Cats, string? MobileMatchedOn,
    string RecordStatus, string RegistrationChannel);

/// <summary>No orders, invoices or balances fields exist -- §4.3.</summary>
public sealed record DuplicateCustomerGroupViewModel(IReadOnlyList<DuplicateCustomerMemberViewModel> Members);
