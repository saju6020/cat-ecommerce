using CatEcommerce.Domain.Exceptions;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>Shorthand for the one exception every Inventory aggregate raises for a broken invariant (§8.14: "reuse
/// <c>DomainRuleViolationException</c>. No new type").</summary>
internal static class InventoryViolation
{
    public static DomainRuleViolationException Of(string code, string message, string propertyName) => new(code, message, propertyName);

    public static string Normalise(string? text) => text?.Trim() ?? string.Empty;
}
