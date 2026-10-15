using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One named arrow of the stay status machine. <see cref="Phrase"/> completes "A {state} stay cannot be ..." in the refusal message.</summary>
public sealed record StayTransition(string Action, IReadOnlyList<BoardingStayStatus> From, BoardingStayStatus To, string Phrase);

/// <summary>
/// HO-BRD-001 section 8.5 (ADR-218, renumbered ADR-351): Appendix C's states and the arrows between them, as ONE table used by the aggregate
/// (<see cref="EnsureAllowed"/>) and by the read side's <c>allowedTransitions[]</c> (<see cref="AllowedActions"/>). A lapse is
/// <c>Cancelled</c> with a reason, not a state of its own.
/// </summary>
public static class StayTransitionRules
{
    public const string Reserve = nameof(Reserve);
    public const string Confirm = nameof(Confirm);
    public const string Lapse = nameof(Lapse);
    public const string ExpireEnquiry = nameof(ExpireEnquiry);
    public const string Cancel = nameof(Cancel);
    public const string CheckIn = nameof(CheckIn);
    public const string NoShow = nameof(NoShow);
    public const string CheckOut = nameof(CheckOut);

    public static readonly IReadOnlyList<StayTransition> All =
    [
        new(Reserve, [BoardingStayStatus.Enquiry], BoardingStayStatus.Reserved, "reserved"),
        new(Confirm, [BoardingStayStatus.Reserved], BoardingStayStatus.Confirmed, "confirmed"),
        new(Lapse, [BoardingStayStatus.Reserved], BoardingStayStatus.Cancelled, "lapsed"),
        new(ExpireEnquiry, [BoardingStayStatus.Enquiry], BoardingStayStatus.Cancelled, "expired"),
        new(Cancel, [BoardingStayStatus.Enquiry, BoardingStayStatus.Reserved, BoardingStayStatus.Confirmed], BoardingStayStatus.Cancelled, "cancelled"),
        new(CheckIn, [BoardingStayStatus.Confirmed], BoardingStayStatus.CheckedIn, "checked in"),
        new(NoShow, [BoardingStayStatus.Confirmed], BoardingStayStatus.NoShow, "marked as a no-show"),
        new(CheckOut, [BoardingStayStatus.CheckedIn], BoardingStayStatus.CheckedOut, "checked out"),
    ];

    public static StayTransition Find(string action) =>
        All.FirstOrDefault(t => t.Action == action) ?? throw new ArgumentException($"'{action}' is not a stay action.", nameof(action));

    public static bool IsAllowed(BoardingStayStatus from, string action) => Find(action).From.Contains(from);

    /// <summary>Throws <c>InvalidStayTransition</c>, naming the state and what was attempted (<c>A cancelled stay cannot be checked in.</c>).</summary>
    public static StayTransition EnsureAllowed(BoardingStayStatus from, string action)
    {
        var transition = Find(action);
        if (!transition.From.Contains(from))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, BoardingMessages.InvalidTransition(from.DisplayName, transition.Phrase), "Status");

        return transition;
    }

    /// <summary>The actions open to a stay in <paramref name="status"/>, in table order -- the read side's <c>allowedTransitions[]</c>.</summary>
    public static IReadOnlyList<string> AllowedActions(BoardingStayStatus status) =>
        All.Where(t => t.From.Contains(status)).Select(t => t.Action).ToList();

    /// <summary>
    /// Whether an allocation (and the stay's cats) holds its unit and cat in the exclusion constraints (section 8.4). True from Reserved to
    /// CheckedOut -- a checked-out range stays active so the turnaround after a departure is still enforced; false for an enquiry, a cancelled
    /// stay and a no-show.
    /// </summary>
    public static bool HoldsOccupancy(BoardingStayStatus status) =>
        status == BoardingStayStatus.Reserved || status == BoardingStayStatus.Confirmed
        || status == BoardingStayStatus.CheckedIn || status == BoardingStayStatus.CheckedOut;
}
