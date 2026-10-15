using Microsoft.AspNetCore.Mvc;

namespace CatEcommerce.Api.Idempotency;

/// <summary>
/// HO-ORD-001 §8.13 (ADR-202). Marks a money-recording or placement action as idempotent at the HTTP edge: <see cref="IdempotencyFilter"/> requires
/// an <c>Idempotency-Key</c> header (a UUID), claims <c>(caller, route, key)</c> in its own short transaction, runs the action once and replays the
/// stored response to a repeat. Independent of the authorisation attributes -- it composes with them.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : TypeFilterAttribute
{
    public IdempotentAttribute() : base(typeof(IdempotencyFilter))
    {
    }
}
