using CatEcommerce.Api.Contracts.Order;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ORD-001 §12.4 -- the request-body parsing the customer and staff return endpoints share: enumeration names become enumerations or a <c>400</c> naming the field.</summary>
internal static class ReturnControllerSupport
{
    public static bool TryMapLines(IReadOnlyList<ReturnLineRequest>? lines, out List<RequestedReturnLine> mapped, out string? error)
    {
        mapped = [];
        error = null;
        if (lines is null || lines.Count == 0) { error = "lines is required."; return false; }

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!EnumQueryParameter.TryParse<ReturnReason>(line.Reason, out var reason) || reason is null) { error = $"lines[{i}].reason is not a return reason."; return false; }
            if (!EnumQueryParameter.TryParse<ItemCondition>(line.Condition, out var condition) || condition is null) { error = $"lines[{i}].condition must be Sealed, Opened or Used."; return false; }

            mapped.Add(new RequestedReturnLine(line.OrderLineId, line.Quantity, reason, condition, line.CustomerNote));
        }

        return true;
    }
}
