namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-PHM-001 §12.2 -- the fixed system-actor id the <c>PharmacyWorker</c> uses as <c>UserContext.UserId</c> when no human caller exists (its own file, beside
/// <see cref="SystemActors"/>, so Pharmacy edits no other area's constants).</summary>
public static class PharmacySystemActors
{
    public static readonly Guid PharmacyWorker = new("00000000-0000-0000-0000-000000000ff5");
}
