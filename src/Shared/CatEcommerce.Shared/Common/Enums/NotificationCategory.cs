namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// Owned by HO-ACC-001 §10.1. "Reused, never duplicated" by every later handover (HO-NTF-001 §10.1).
/// Created here, ahead of Accounts' own build, because HO-NTF-001's template catalogue (this
/// contracts pass has no fake for it, but the type is referenced by <c>NotificationRequestDto</c>'s
/// neighbours) needs it.
/// </summary>
public sealed class NotificationCategory(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly NotificationCategory Transactional = new(1, nameof(Transactional));
    public static readonly NotificationCategory Marketing = new(2, nameof(Marketing));
}
