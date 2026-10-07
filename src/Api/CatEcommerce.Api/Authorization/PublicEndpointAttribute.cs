namespace CatEcommerce.Api.Authorization;

/// <summary>
/// HO-ADM-001 §12.3 / ADR-043. A deliberate marker -- "this action is intentionally reachable
/// without <c>[RequireAction]</c> or <c>[RequireCustomerSession]</c>", e.g. CAPTCHA, registration,
/// sign-in, password reset and session-read endpoints, and every public storefront read this slice
/// adds. Carries no behaviour of its own; <c>EndpointAccessConventionTests</c> (U-A1) reflects over
/// every controller action in the Api assembly and fails the build if an action carries none of
/// <see cref="RequireActionAttribute"/>, <c>RequireCustomerSessionAttribute</c> or this attribute --
/// the whole point being that an endpoint's audience is never left merely implicit.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PublicEndpointAttribute : Attribute;
