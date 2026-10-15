using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One requested line with everything the policy needs to judge it. <c>BatchRecalledOrDefective</c> is true when any batch the line was allocated from is
/// recalled or carries a quality defect recorded by Inventory (BR-10's exception).</summary>
public sealed record ReturnLineEligibilityInput(
    Guid OrderLineId, string ProductName, int OrderedQuantity, int AlreadyReturned, int OpenInOtherReturns, ProductReturnClass ReturnClass, bool BatchRecalledOrDefective,
    int RequestedQuantity, ReturnReason Reason, ItemCondition Condition);

public sealed record ReturnEligibilityInput(
    OrderStatus OrderStatus, DateOnly? DeliveredLocalDate, DateOnly TodayLocal, int WindowDays, IReadOnlyList<ReturnLineEligibilityInput> Lines);

/// <summary>The verdict on one line: eligible (with whether approval must record a confirmed defect), or refused with a code and a plain message.</summary>
public sealed record ReturnLineVerdict(Guid OrderLineId, bool Eligible, bool RequiresDefectConfirmation, string? Code, string? Message);

/// <summary>
/// HO-ORD-001 §8.10 (ADR-210), US-ORD-13, BR-10 -- who may send what back, as a pure function. In order, per line: the order was delivered and today (branch-local) is
/// no later than <c>DeliveredOn + returnWindowDays</c> (<c>ReturnWindowClosed</c>); the quantity is no more than delivered less returned less already asked
/// for (<c>ReturnQuantityExceeded</c>); then <b>BR-10</b> -- food that is not sealed, or used litter, is refused (<c>ReturnNotEligibleOpenedFood</c> /
/// <c>ReturnNotEligibleUsedLitter</c>) <b>unless</b> an allocated batch is recalled or defective (then eligible outright) or the reason is a quality defect (then
/// eligible <i>for approval</i> with a defect to confirm). Anything else is eligible and goes to staff approval.
/// </summary>
public static class ReturnEligibilityPolicy
{
    public static IReadOnlyList<ReturnLineVerdict> Evaluate(ReturnEligibilityInput input) => input.Lines.Select(line => EvaluateLine(input, line)).ToList();

    private static ReturnLineVerdict EvaluateLine(ReturnEligibilityInput input, ReturnLineEligibilityInput line)
    {
        if (input.OrderStatus != OrderStatus.Delivered || input.DeliveredLocalDate is not { } delivered)
            return Refused(line, BusinessViolationCodes.ReturnWindowClosed, "Only items that have been delivered can be returned.");

        if (input.TodayLocal > delivered.AddDays(input.WindowDays))
            return Refused(line, BusinessViolationCodes.ReturnWindowClosed, $"The {input.WindowDays}-day return period for '{line.ProductName}' has ended.");

        var returnable = line.OrderedQuantity - line.AlreadyReturned - line.OpenInOtherReturns;
        if (line.RequestedQuantity < 1 || line.RequestedQuantity > returnable)
            return Refused(line, BusinessViolationCodes.ReturnQuantityExceeded, returnable <= 0
                ? $"Nothing more of '{line.ProductName}' can be returned."
                : $"You can return up to {returnable} of '{line.ProductName}'.");

        var openedFood = line.ReturnClass == ProductReturnClass.Food && line.Condition != ItemCondition.Sealed;
        var usedLitter = line.ReturnClass == ProductReturnClass.Litter && line.Condition == ItemCondition.Used;
        if (!openedFood && !usedLitter) return new ReturnLineVerdict(line.OrderLineId, true, false, null, null);

        if (line.BatchRecalledOrDefective) return new ReturnLineVerdict(line.OrderLineId, true, false, null, null);
        if (line.Reason == ReturnReason.QualityDefect) return new ReturnLineVerdict(line.OrderLineId, true, true, null, null);

        return openedFood
            ? Refused(line, BusinessViolationCodes.ReturnNotEligibleOpenedFood, $"'{line.ProductName}' has been opened, so it can only be returned if it is faulty or recalled.")
            : Refused(line, BusinessViolationCodes.ReturnNotEligibleUsedLitter, $"'{line.ProductName}' has been used, so it can only be returned if it is faulty or recalled.");
    }

    private static ReturnLineVerdict Refused(ReturnLineEligibilityInput line, string code, string message) => new(line.OrderLineId, false, false, code, message);
}
