namespace CatEcommerce.Domain.Models;

/// <summary>One invoice line's share of an amount-based credit, in gross money (Billing splits net and tax at the line's frozen rate).</summary>
public sealed record CreditLinePlan(Guid InvoiceLineId, decimal Amount);

/// <summary>Billing's plan for crediting an amount against an invoice: either a <see cref="Full"/> credit of what is left, or the per-line amounts; or a refusal
/// (<see cref="RefusalCode"/>, one of Billing's own violation codes) when the amount is more than is left to credit.</summary>
public sealed record CreditPlan(bool Full, IReadOnlyList<CreditLinePlan> Lines, string? RefusalCode, string? RefusalMessage)
{
    public bool IsRefused => RefusalCode is not null;
}

/// <summary>Money to pay back from one payment.</summary>
public sealed record RefundDraw(Guid PaymentId, decimal Amount);

/// <summary>Which payments an amount is paid back from, in the order given, each capped at what the payment still holds free; <see cref="Shortfall"/> is what no
/// payment could cover.</summary>
public sealed record RefundDrawPlan(IReadOnlyList<RefundDraw> Draws, decimal Shortfall);
