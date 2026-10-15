namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// HO-XCT-001 gate fix 2026-10-12 (SECURITY medium). How a member of staff verified who was asking for a staff-taken erasure. A <b>fixed list of codes</b>,
/// never free text: the code is written to the immutable audit trail, and a free-text field there would invite staff to type the very identifiers
/// (ID numbers, dates of birth, phone numbers) whose erasure the row records.
/// </summary>
public sealed class ErasureIdentityCheck(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ErasureIdentityCheck RegisteredMobileCallback = new(1, nameof(RegisteredMobileCallback));
    public static readonly ErasureIdentityCheck RegisteredEmailReply = new(2, nameof(RegisteredEmailReply));
    public static readonly ErasureIdentityCheck InPersonPhotoId = new(3, nameof(InPersonPhotoId));
    public static readonly ErasureIdentityCheck WrittenRequestSigned = new(4, nameof(WrittenRequestSigned));
}
