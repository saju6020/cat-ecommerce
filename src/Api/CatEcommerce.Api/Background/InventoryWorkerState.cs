namespace CatEcommerce.Api.Background;

/// <summary>Process-lifetime state of the inventory worker: when the daily reconciliation last ran (HO-IMP-001 section 12.2, "once per 24 h of process uptime").</summary>
public sealed class InventoryWorkerState
{
    private readonly object _gate = new();
    private DateTimeOffset? _lastReconciliation;

    /// <summary>True, and records <paramref name="now"/>, when reconciliation has not run in the last 24 hours of this process.</summary>
    public bool TryStartReconciliation(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_lastReconciliation is { } last && now - last < TimeSpan.FromHours(24)) return false;
            _lastReconciliation = now;
            return true;
        }
    }
}
