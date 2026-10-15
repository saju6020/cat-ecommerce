namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-BIL-001 §8.10 -- the expected totals of a cash-up, frozen when it closes: per method total and
/// count, cash refunds, cash reversals, carried-in amounts and the expected cash itself (the same
/// <c>CashUpCalculator</c> the read side uses, so the number the cashier saw is the number frozen).
/// </summary>
public sealed record CashUpSnapshot
{
    public decimal CashTotal { get; init; }
    public int CashCount { get; init; }
    public decimal CardTotal { get; init; }
    public int CardCount { get; init; }
    public decimal TransferTotal { get; init; }
    public int TransferCount { get; init; }
    public decimal CashRefunds { get; init; }
    public decimal CashReversals { get; init; }
    public decimal CarriedIn { get; init; }
    public decimal ExpectedCash { get; init; }

    public CashUpSnapshot(
        decimal cashTotal, int cashCount, decimal cardTotal, int cardCount, decimal transferTotal, int transferCount,
        decimal cashRefunds, decimal cashReversals, decimal carriedIn, decimal expectedCash)
    {
        CashTotal = cashTotal;
        CashCount = cashCount;
        CardTotal = cardTotal;
        CardCount = cardCount;
        TransferTotal = transferTotal;
        TransferCount = transferCount;
        CashRefunds = cashRefunds;
        CashReversals = cashReversals;
        CarriedIn = carriedIn;
        ExpectedCash = expectedCash;
    }

    private CashUpSnapshot()
    {
    }
}
