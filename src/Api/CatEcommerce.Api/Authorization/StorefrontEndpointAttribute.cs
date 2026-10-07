namespace CatEcommerce.Api.Authorization;

/// <summary>
/// HO-ADM-001 §12.5 / ADR-053. Marks a customer-facing (storefront) action so
/// <see cref="CatEcommerce.Api.Filters.MaintenanceModeFilter"/> answers it with <c>503</c> while
/// maintenance mode is on. Independent of <see cref="RequireActionAttribute"/>,
/// <see cref="RequireCustomerSessionAttribute"/> and <see cref="PublicEndpointAttribute"/> -- an
/// action carries exactly one of those three for authorization, and this in addition, only when it
/// is a storefront action. No action in this slice carries it yet (no storefront-owning handover
/// has landed); a later handover applies it to its own actions.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class StorefrontEndpointAttribute : Attribute;
