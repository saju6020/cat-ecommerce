namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-NTF-001 §10.3 -- fixed system-actor ids used as <c>UserContext.UserId</c> when no
/// human caller exists (the worker, the anonymous unsubscribe endpoint).</summary>
public static class SystemActors
{
    public static readonly Guid NotificationWorker = new("00000000-0000-0000-0000-000000000ffe");
}
