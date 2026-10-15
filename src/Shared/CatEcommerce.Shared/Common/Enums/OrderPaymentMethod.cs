namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. How the order is paid (no gateway in v1: SRS A-2, C-5).</summary>
public sealed class OrderPaymentMethod(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly OrderPaymentMethod CashOnDelivery = new(1, nameof(CashOnDelivery));
    public static readonly OrderPaymentMethod BankTransfer = new(2, nameof(BankTransfer));
    public static readonly OrderPaymentMethod PayOnCollection = new(3, nameof(PayOnCollection));
    public static readonly OrderPaymentMethod InStore = new(4, nameof(InStore));
}
