namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-APT-001 §10.3.</summary>
public static class AppointmentRules
{
    public const int MaxHoldsPerCustomer = 1;
    public const int MaxReasonLength = 500;
    public const int MaxCalendarRangeDays = 7;
    public const int MaxSlotRangeDays = 1;

    /// <summary>§8.2 -- an owned collection an aggregate method appends to; kept bounded so the
    /// aggregate never loads an unbounded history (.ai/persistence-ef-core.md §4).</summary>
    public const int MaxStatusHistoryEntries = 20;
}
