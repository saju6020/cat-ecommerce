using System.Collections;
using System.Reflection;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Filters;

/// <summary>
/// Gate fix 2026-09-26 (SEC LOW, hardening round). One API-wide guard, replacing a per-field
/// whack-a-mole: rejects a NUL byte (U+0000) anywhere in a bound request -- route values, query
/// values and, recursively, every string reachable from a `[FromBody]` object -- for every
/// controller action in the app, not just Administration's own. A NUL byte survives JSON
/// deserialization (it is a legal, if unusual, JSON string character once escaped as
/// <c>\u0000</c>) and, with no format or <c>NoControlCharacters</c> rule of its own, previously
/// reached Postgres unfiltered -- an uncaught 500 (22021), one field at a time as each was probed.
///
/// Registered as a global <c>MvcOptions</c> filter from <c>AdministrationModule</c> (the same
/// precedent <see cref="MaintenanceModeFilter"/> already set) rather than in <c>Program.cs</c> --
/// <c>MvcOptions.Filters</c> applies to every controller in the host regardless of which module
/// registered it, so this closes the whole class of "a free-text field with no rule of its own"
/// everywhere, not only in Administration's controllers. The existing per-field FluentValidation
/// rules (<c>NoControlCharacters</c>, <c>SlugFormat</c>, <c>CodeFormat</c>) are unchanged and still
/// run for the commands that carry them -- this filter is a backstop, not a replacement, since it
/// runs before the platform's own validation pipeline and gives no field-specific error code.
///
/// Recursion is bounded to <see cref="MaxDepth"/> and only descends into this solution's own types
/// (namespace starting <c>CatEcommerce</c>) plus plain enumerables -- a request DTO's own shape --
/// so it never reflects into framework types (<c>HttpContext</c>, <c>CancellationToken</c>, and so
/// on) that model binding can also hand a controller action.
/// </summary>
public sealed class NulByteRejectionFilter : IActionFilter
{
    private const int MaxDepth = 8;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (!ContainsNulByte(argument, depth: 0)) continue;

            var validationResponse = new ValidationResponse();
            validationResponse.AddError(
                errorMessage: "must not contain a NUL character.",
                propertyName: "",
                errorCode: BusinessViolationCodes.ContainsControlCharacter,
                resourceName: "");
            context.Result = new BadRequestObjectResult(validationResponse);
            return;
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    private static bool ContainsNulByte(object? value, int depth)
    {
        if (value is null || depth > MaxDepth) return false;

        if (value is string text) return text.Contains('\0');

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (ContainsNulByte(item, depth + 1)) return true;
            }

            return false;
        }

        var type = value.GetType();
        if (type.Namespace is null || !type.Namespace.StartsWith("CatEcommerce", StringComparison.Ordinal)) return false;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;

            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch
            {
                continue;
            }

            if (ContainsNulByte(propertyValue, depth + 1)) return true;
        }

        return false;
    }
}
