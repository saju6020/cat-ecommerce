namespace CatEcommerce.Api.Filters;

/// <summary>
/// HO-BRD-001 section 12.5 (ADR-219, renumbered ADR-352): marks an endpoint that takes a unit or a cat through the stay exclusion constraints (reserve,
/// extend, relocate, flag-and-relocate). Under heavy concurrent inserts Postgres may report a deadlock (40P01) for some losers instead of a clean
/// exclusion violation (23P01); on these endpoints, and only these, the constraint filter answers it as the 409 a lost race is. Everywhere else a deadlock
/// stays the logged 500 it is.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class BoardingClaimEndpointAttribute : Attribute;
