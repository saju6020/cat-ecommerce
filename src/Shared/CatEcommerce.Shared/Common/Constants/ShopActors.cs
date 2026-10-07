namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-SHP-001 section 10.3 -- the fixed actor id <c>ShopWorker</c> runs under (the same
/// pattern as <see cref="SystemActors.NotificationWorker"/> and the Clinic worker's), so a worker
/// pass is attributable in audit rows.</summary>
public static class ShopActors
{
    public static readonly Guid Worker = new("00000000-0000-0000-0000-000000000ff2");
}
