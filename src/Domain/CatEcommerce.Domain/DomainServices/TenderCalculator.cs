using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One tender the cashier took at the counter: the money handed over (cash) or charged on the terminal (card), and the terminal slip reference of a card.</summary>
public sealed record CounterTender(TenderMethod Method, MinorUnits Amount, string? Reference);

/// <summary>One payment to record against the sale: a card at face value, or the cash that is actually due (what was handed over less the change).</summary>
public sealed record TenderPayment(TenderMethod Method, MinorUnits Amount, string? Reference);

/// <summary>The verdict on a set of tenders: the payments to record and the change due, or the refusal (<c>ViolationCode</c>) with its message.</summary>
public sealed record TenderOutcome(IReadOnlyList<TenderPayment> Payments, MinorUnits ChangeDue, string? ViolationCode, string? Message)
{
    public bool IsSuccess => ViolationCode is null;
}

/// <summary>
/// HO-ORD-001 §8.11 (ADR-212), US-ORD-16 AC1 -- the counter's tender rule, pure. Cards are taken at face value and may not exceed what is still owed (there is no
/// cash-back on a card); every card needs its terminal slip reference (<c>TerminalReferenceRequired</c>, C-5 -- a reference, never a card number); the cash handed over
/// must cover the rest and the difference is the change. Anything else is <c>TendersDoNotCoverTotal</c>. Cash is recorded for the amount <b>due</b>, not the amount
/// handed over, so the payments add up to the grand total exactly.
/// </summary>
public static class TenderCalculator
{
    public const int MaxTenders = 6;
    public const int MaxReferenceLength = 50;

    public static TenderOutcome Evaluate(MinorUnits grandTotal, IReadOnlyList<CounterTender> tenders)
    {
        if (tenders.Count == 0 || tenders.Count > MaxTenders || tenders.Any(t => t.Amount <= MinorUnits.Zero))
            return Refuse(BusinessViolationCodes.TendersDoNotCoverTotal, "Take at least one payment, each above zero.");

        var cards = tenders.Where(t => t.Method == TenderMethod.InStoreCard).ToList();
        if (cards.Any(c => string.IsNullOrWhiteSpace(c.Reference)))
            return Refuse(BusinessViolationCodes.TerminalReferenceRequired, "Enter the card terminal slip reference.");

        if (cards.Any(c => c.Reference!.Trim().Length > MaxReferenceLength))
            return Refuse(BusinessViolationCodes.TerminalReferenceRequired, $"The slip reference can be up to {MaxReferenceLength} characters.");

        var cardTotal = new MinorUnits(cards.Sum(c => c.Amount.Value));
        if (cardTotal > grandTotal)
            return Refuse(BusinessViolationCodes.TendersDoNotCoverTotal, "Card payments cannot be more than what is owed.");

        var cashDue = grandTotal - cardTotal;
        var cashHanded = new MinorUnits(tenders.Where(t => t.Method == TenderMethod.Cash).Sum(t => t.Amount.Value));
        if (cashHanded < cashDue)
            return Refuse(BusinessViolationCodes.TendersDoNotCoverTotal, "The payments do not cover the total of the sale.");

        var payments = cards.Select(c => new TenderPayment(TenderMethod.InStoreCard, c.Amount, c.Reference!.Trim())).ToList();
        if (cashDue > MinorUnits.Zero) payments.Add(new TenderPayment(TenderMethod.Cash, cashDue, null));

        return new TenderOutcome(payments, cashHanded - cashDue, null, null);
    }

    private static TenderOutcome Refuse(string code, string message) => new([], MinorUnits.Zero, code, message);
}
