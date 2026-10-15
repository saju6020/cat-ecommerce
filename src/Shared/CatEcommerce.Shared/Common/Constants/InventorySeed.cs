namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-IMP-001 §8.3 -- the five stock locations the migration seeds for
/// <see cref="AdministrationSeed.MainBranchId"/>. Fixed ids so tests and later migrations can name them.</summary>
public static class InventorySeed
{
    public static readonly Guid ShopLocationId = Guid.Parse("00000000-0000-0000-0000-0000000051a1");
    public static readonly Guid WarehouseLocationId = Guid.Parse("00000000-0000-0000-0000-0000000051a2");
    public static readonly Guid DispensaryLocationId = Guid.Parse("00000000-0000-0000-0000-0000000051a3");
    public static readonly Guid BoardingStoreLocationId = Guid.Parse("00000000-0000-0000-0000-0000000051a4");
    public static readonly Guid QuarantineLocationId = Guid.Parse("00000000-0000-0000-0000-0000000051a5");

    public const string ShopCode = "SHOP";
    public const string WarehouseCode = "WH";
    public const string DispensaryCode = "DISP";
    public const string BoardingStoreCode = "BRD";
    public const string QuarantineCode = "QUAR";
}
