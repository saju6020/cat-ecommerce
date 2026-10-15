namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Cat Profiles (HO-CAT-001) is exempted from <c>IPersonalDataEraser</c> registration (HO-XCT-001 section 8.8, which names it): a cat
/// keeps the owner account id (<c>OwnerId</c>) and nothing else personal -- the cat is not a person, and every personal field of the owner lives
/// in the Account context, which is anonymised in place. Added by HO-XCT-001 (the handover assigns the exemption list to it); no Cat Profiles code
/// changed.</summary>
public static partial class ErasureExemptions
{
    private static readonly IReadOnlyList<(string ContextKey, string Reason)> CatProfilesEntries =
    [
        ("cat", "Owner account id only (OwnerId); the cat is not a person and the owner personal fields live in Account, which is anonymised in place (HO-XCT-001 section 8.8)."),
    ];
}
