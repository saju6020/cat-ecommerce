using System.Text.Json;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Operations;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-XCT-001 section 12.4 (US-XCT-02, ADR-309). The anonymous web-vitals beacon. Public, CSRF-exempt (a beacon cannot send the header, and treating a stale
/// session as anonymous is exactly what is wanted), rate limited per IP, capped at 4 KB, and parsed as JSON whatever the content type (a beacon is
/// <c>text/plain</c>). It stores no identity even when the caller is signed in, and logs none.
/// </summary>
[ApiController]
public sealed class TelemetryController(IInMemoryDispatcher dispatcher) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [HttpPost("api/telemetry/web-vitals")]
    [PublicEndpoint]
    [CsrfExempt]
    [EnableRateLimiting(RateLimitPolicies.Telemetry)]
    public async Task<IActionResult> RecordWebVitals(CancellationToken ct)
    {
        if (Request.ContentLength > WebVitalBudgets.MaxPayloadBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge);

        var buffer = new byte[WebVitalBudgets.MaxPayloadBytes + 1];
        var read = 0;
        int count;
        while (read < buffer.Length && (count = await Request.Body.ReadAsync(buffer.AsMemory(read), ct)) > 0) read += count;
        if (read > WebVitalBudgets.MaxPayloadBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge);

        WebVitalsRequest? request;
        try { request = JsonSerializer.Deserialize<WebVitalsRequest>(buffer.AsSpan(0, read), Json); }
        catch (JsonException) { return Invalid(); }
        if (request?.Samples is null) return Invalid();

        var response = await dispatcher.SendAsync(new RecordWebVitalsCommand
        {
            BatchId = Guid.NewGuid(),
            BuildId = request.BuildId ?? string.Empty,
            Samples = request.Samples.Select(s => new WebVitalSampleInput(
                s.Metric ?? string.Empty, s.Value, s.RouteTemplate ?? string.Empty, s.NavigationType ?? string.Empty, s.EffectiveConnectionType, s.ViewportClass ?? string.Empty)).ToList(),
            // Never the caller's identity, even when a customer or staff cookie came with the request.
            UserContext = new UserContext(SystemActors.Anonymous, SystemActors.Anonymous), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : Invalid();
    }

    /// <summary>One generic error whatever was wrong, so the endpoint is not an oracle.</summary>
    private IActionResult Invalid()
    {
        var validation = new ValidationResponse();
        validation.AddError(
            errorMessage: OperationsMessages.InvalidWebVitalsPayload, propertyName: "payload",
            errorCode: BusinessViolationCodes.InvalidWebVitalsPayload, resourceName: nameof(TelemetryController));
        return BadRequest(validation);
    }
}
