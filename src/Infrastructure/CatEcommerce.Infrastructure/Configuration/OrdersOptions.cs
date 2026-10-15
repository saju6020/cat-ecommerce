namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>HO-ORD-001 §12.2. Bound from configuration section "Orders": the worker that expires unpaid transfer orders and purges stale guest carts and
/// idempotency records. Disabled in the <c>Testing</c> environment; tests call <c>OrderWorkCycle</c> directly.</summary>
public sealed class OrdersOptions
{
    public const string SectionName = "Orders";

    public OrdersWorkerOptions Worker { get; set; } = new();
}

public sealed class OrdersWorkerOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
}
