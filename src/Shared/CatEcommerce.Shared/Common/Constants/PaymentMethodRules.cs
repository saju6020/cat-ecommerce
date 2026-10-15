using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Where a payment's money is listed on the till sheet (§8.10): counted cash, or listed separately.</summary>
public enum TillBucket
{
    None,
    Cash,
    Card,
    Transfer,
}

/// <summary>
/// HO-BIL-001 §8.8.1 (ADR-182). <b>The only place that reads a <see cref="PaymentMethod"/>'s
/// identity.</b> Everything else reads the capability flags on the method itself
/// (<c>RequiresReference</c>, <c>RequiresCollector</c>, ...). <c>U-X4</c> scans the Application, Read
/// and Api sources for <c>PaymentMethod.&lt;Member&gt;</c> references outside this file.
/// </summary>
public static class PaymentMethodRules
{

    /// <summary>The two methods a reconciliation <i>query</i> filters on, exposed here so no query names a method identity itself (U-X4).</summary>
    public static readonly PaymentMethod CashOnDeliveryMethod = PaymentMethod.CashOnDelivery;
    public static readonly PaymentMethod BankTransferMethod = PaymentMethod.BankTransfer;
    /// <summary>§8.10: cash in store counts in the till; cash on delivery only when staff collected it.</summary>
    public static bool CountsInTill(PaymentMethod method, CollectorKind? collectorKind) =>
        method.CountsInTill || (method.CountsInTillWhenStaffCollected && collectorKind == CollectorKind.Staff);

    public static TillBucket BucketOf(PaymentMethod method, CollectorKind? collectorKind)
    {
        if (CountsInTill(method, collectorKind)) return TillBucket.Cash;
        if (method == PaymentMethod.CardInStore) return TillBucket.Card;
        if (method == PaymentMethod.BankTransfer) return TillBucket.Transfer;
        return TillBucket.None;
    }

    /// <summary>§8.9 / C25: only a bank transfer matches a bank statement line.</summary>
    public static bool IsBankTransfer(PaymentMethod method) => method == PaymentMethod.BankTransfer;

    /// <summary>§11.4 C22: only cash on delivery is remitted by a courier.</summary>
    public static bool IsCashOnDelivery(PaymentMethod method) => method == PaymentMethod.CashOnDelivery;

    /// <summary>FU1 D4 (ORD-53): the second approval may be waived only for the methods Ordering uses for a cash-on-delivery collection (<see cref="ApprovalWaiverReasons.CodCollection"/>)
    /// or a counter tender (<see cref="ApprovalWaiverReasons.CounterTender"/>: cash or card in store). Never a bank transfer.</summary>
    public static bool CanWaiveApproval(PaymentMethod method, string? reason) => reason switch
    {
        ApprovalWaiverReasons.CodCollection => method == PaymentMethod.CashOnDelivery,
        ApprovalWaiverReasons.CounterTender => method == PaymentMethod.CashInStore || method == PaymentMethod.CardInStore,
        _ => false,
    };

    public static bool TryParse(string? name, out PaymentMethod? method) => EnumerationParsing.TryParse(name, out method);

    public static bool IsAllowedRefundMethod(PaymentMethod method, RefundMethod refundMethod) =>
        method.AllowedRefundMethods.Contains(refundMethod);
}

/// <summary>FU1 D4 (ORD-52): why a payment skipped the second approval. Stored on the payment and written to the audit trail.</summary>
public static class ApprovalWaiverReasons
{
    public const string CodCollection = nameof(CodCollection);
    public const string CounterTender = nameof(CounterTender);
}
