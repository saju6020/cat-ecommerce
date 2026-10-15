namespace CatEcommerce.Api.Filters;

/// <summary>
/// HO-ORD-001 §12.5 -- endpoint metadata read by <see cref="ConcurrencyExceptionFilter"/>: on the placement route a <c>DbUpdateConcurrencyException</c>
/// (a discount code's or the cart's <c>xmin</c> moved under the quote) is answered <c>409 CheckoutQuoteChanged</c> instead of "reload", so the client
/// re-quotes and the customer sees what changed.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ConflictAsQuoteChangedAttribute : Attribute;
