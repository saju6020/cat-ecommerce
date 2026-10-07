namespace CatEcommerce.Api.Authorization;

/// <summary>HO-ACC-001 §12.4 / ADR-037. Marks a staff endpoint that must not record activity
/// (e.g. <c>GET /api/staff/session</c> must never extend the idle window just by being polled).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PassiveSessionAttribute : Attribute;
