using System.Collections.Concurrent;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Idempotency;
using Microsoft.AspNetCore.Mvc;

namespace CatEcommerce.IntegrationTests.Ordering;

/// <summary>
/// A controller that exists only in the test assembly (added as an application part by <see cref="OrderingApiFactory"/>), so HO-ORD-001's
/// <c>[Idempotent]</c> edge filter (ADR-202, U-X2) is driven by real requests before any real endpoint carries it. Each call counts its
/// executions per <c>name</c>, so a test can prove the action ran once for a repeated key.
/// </summary>
[ApiController]
[Route("api/test-probe/idempotency")]
[PublicEndpoint]
public sealed class IdempotencyProbeController : ControllerBase
{
    public static readonly ConcurrentDictionary<string, int> Executions = new();
    public static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();

    public sealed record ProbeBody(string Value, int? Status = null, bool Fail = false, bool WaitForGate = false);

    [HttpPost("{name}")]
    [Idempotent]
    public async Task<IActionResult> Post(string name, [FromBody] ProbeBody body)
    {
        Executions.AddOrUpdate(name, 1, (_, count) => count + 1);

        if (body.WaitForGate)
            await Gates.GetOrAdd(name, _ => new SemaphoreSlim(0)).WaitAsync(TimeSpan.FromSeconds(20));

        if (body.Fail) throw new InvalidOperationException("probe failure");
        if (body.Status is { } status) return StatusCode(status, new { value = body.Value, executed = Executions[name] });

        return StatusCode(201, new { id = Guid.NewGuid(), value = body.Value });
    }
}
