using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One refund method as the refund screen shows it: allowed by how the customer paid, and available (store credit is not, until Billing builds it).</summary>
public sealed record RefundMethodOption(OrderRefundMethod Method, bool AllowedForPayment, bool Available, string? UnavailableReason);

/// <summary>
/// HO-ORD-001 §8.6 (ADR-211), US-ORD-15 AC1 -- the refund methods by how the customer paid, as a pure table. Cash (cash on delivery, cash at collection or the
/// counter) and an in-store card (no card refund path exists without a gateway, OQ-9) can be refunded as cash in store, by bank transfer or as store credit; a bank
/// transfer by bank transfer or store credit. An order paid by several tenders allows the union. <c>StoreCredit</c> is allowed by the table but <b>unavailable</b> while
/// Billing offers no store-credit ledger (OQ-8).
/// </summary>
public static class RefundMethodPolicy
{
    public const string StoreCreditUnavailableReason = "RefundMethodUnavailable";

    public static IReadOnlyList<RefundMethodOption> Evaluate(IEnumerable<string> paymentMethods, bool storeCreditAvailable)
    {
        var methods = paymentMethods.ToHashSet(StringComparer.Ordinal);
        var paidInCashOrCard = methods.Overlaps(["CashOnDelivery", "CashInStore", "CardInStore"]);
        var paidByTransfer = methods.Contains("BankTransfer");
        var any = paidInCashOrCard || paidByTransfer;

        return
        [
            new RefundMethodOption(OrderRefundMethod.CashInStore, paidInCashOrCard, paidInCashOrCard, null),
            new RefundMethodOption(OrderRefundMethod.BankTransfer, any, any, null),
            new RefundMethodOption(OrderRefundMethod.StoreCredit, any, any && storeCreditAvailable, any && !storeCreditAvailable ? StoreCreditUnavailableReason : null),
        ];
    }
}

/// <summary>One order line as the suggested-refund calculation needs it (minor units).</summary>
public sealed record RefundLineInput(MinorUnits LineTotal, int Quantity, int AlreadyReturnedBefore, int ReturningNow);

/// <summary>
/// HO-ORD-001 §8.10 (ADR-198), US-ORD-15 -- the amount to suggest for returned goods: for each line, the value of the units returned now, computed by
/// <b>cumulative rounding</b> (<c>round(total x returned / quantity)</c> half away from zero, minus what the earlier returns of the line were worth), so the shares of all
/// the units of a line add up to its total exactly and any remainder lands on the last unit. The delivery charge is never included (OQ-9). Staff may change the amount;
/// no refund is automated (SRS §18.5).
/// </summary>
public static class RefundCalculator
{
    public static MinorUnits Suggested(IEnumerable<RefundLineInput> lines) => new(lines.Sum(l => ValueOf(l).Value));

    public static MinorUnits ValueOf(RefundLineInput line)
    {
        if (line.Quantity <= 0 || line.ReturningNow <= 0) return MinorUnits.Zero;

        var after = Cumulative(line.LineTotal, line.Quantity, Math.Min(line.Quantity, line.AlreadyReturnedBefore + line.ReturningNow));
        var before = Cumulative(line.LineTotal, line.Quantity, Math.Min(line.Quantity, line.AlreadyReturnedBefore));
        return after - before;
    }

    private static MinorUnits Cumulative(MinorUnits total, int quantity, int units) =>
        units >= quantity ? total : new MinorUnits((long)Math.Round((decimal)total.Value * units / quantity, MidpointRounding.AwayFromZero));
}
