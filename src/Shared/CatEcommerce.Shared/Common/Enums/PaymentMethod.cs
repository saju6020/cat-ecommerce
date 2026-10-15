namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// HO-BIL-001 §8.8.1 / §10.1 (ADR-182). The four v1 payment methods, each <b>carrying its behaviour
/// as data</b> -- capability flags -- so nothing outside <c>PaymentMethodRules</c> ever needs to ask
/// "which method is this". No online member exists before the gateway does (SRS Phase 5, A-2,
/// <c>.ai/enumeration-pattern.md</c>); adding one later is a new member with
/// <see cref="IsOnline"/> / <see cref="SupportsAutomatedRefund"/> set, plus a provider adapter.
/// </summary>
public sealed class PaymentMethod : Enumeration
{
    public bool CountsInTill { get; }

    /// <summary>Counted in the till only when the collector is staff (cash on delivery, §8.10).</summary>
    public bool CountsInTillWhenStaffCollected { get; }

    public bool RequiresReference { get; }
    public string ReferenceLabel { get; }
    public bool RequiresCollector { get; }
    public bool IsOnline { get; }
    public bool SupportsAutomatedRefund { get; }
    public IReadOnlyList<RefundMethod> AllowedRefundMethods { get; }

    private PaymentMethod(
        int value, string displayName, bool countsInTill, bool countsInTillWhenStaffCollected,
        bool requiresReference, string referenceLabel, bool requiresCollector, bool isOnline,
        bool supportsAutomatedRefund, params RefundMethod[] allowedRefundMethods)
        : base(value, displayName)
    {
        CountsInTill = countsInTill;
        CountsInTillWhenStaffCollected = countsInTillWhenStaffCollected;
        RequiresReference = requiresReference;
        ReferenceLabel = referenceLabel;
        RequiresCollector = requiresCollector;
        IsOnline = isOnline;
        SupportsAutomatedRefund = supportsAutomatedRefund;
        AllowedRefundMethods = allowedRefundMethods;
    }

    public static readonly PaymentMethod CashInStore = new(1, nameof(CashInStore), true, false, false, "Receipt number", false, false, false, RefundMethod.CashInStore);
    public static readonly PaymentMethod CardInStore = new(2, nameof(CardInStore), false, false, true, "Terminal slip number", false, false, false, RefundMethod.CashInStore);
    public static readonly PaymentMethod BankTransfer = new(3, nameof(BankTransfer), false, false, true, "Transfer reference", false, false, false, RefundMethod.BankTransfer);
    public static readonly PaymentMethod CashOnDelivery = new(4, nameof(CashOnDelivery), false, true, false, "Reference", true, false, false, RefundMethod.CashInStore, RefundMethod.BankTransfer);
}
