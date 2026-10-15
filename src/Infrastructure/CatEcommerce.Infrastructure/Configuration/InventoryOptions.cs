namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>HO-IMP-001 section 12.2. Bound from configuration section "Inventory".</summary>
public sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    public InventoryWorkerOptions Worker { get; set; } = new();
}

public sealed class InventoryWorkerOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 300;
}
