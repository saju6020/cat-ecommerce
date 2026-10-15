using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-ORD-001 §8.13 (ADR-209, COM-3, NFR-SEC-6, US-ORD-18 AC2) -- the courier <b>tracking</b> webhook (there is no payment webhook: no payment gateway in v1,
/// C-5). Public, rate limited (<c>courier-webhook</c>), authenticated by signature alone: the provider must be registered in <c>Couriers:Webhooks:{provider}</c>
/// (else <c>404</c>) and the body's HMAC-SHA256 under the provider's secret must match the signature header, compared in fixed time (else <c>401</c>, a
/// <c>Warning</c> naming the provider only -- <b>the body is never logged or stored</b>). The body is this slice's generic tracking contract
/// (<c>eventId</c>, <c>trackingNumber</c>, <c>status</c>, <c>occurredOn</c>) until a real courier's mapper is written (Q-4). A duplicate event id is <c>200</c> and
/// nothing else happens.
/// </summary>
[ApiController]
[Route("api/webhooks/couriers/{provider}")]
public sealed class CourierWebhooksController(
    IInMemoryDispatcher dispatcher, IOptionsMonitor<CouriersOptions> options, ILogger<CourierWebhooksController> logger) : ControllerBase
{
    private const int MaxBodyBytes = 65_536;

    [HttpPost]
    [PublicEndpoint]
    [EnableRateLimiting(RateLimitPolicies.CourierWebhook)]
    public async Task<IActionResult> Receive(string provider)
    {
        if (!options.CurrentValue.Webhooks.TryGetValue(provider, out var configured) || string.IsNullOrEmpty(configured.Secret)) return NotFound();

        var body = await ReadBodyAsync(Request);
        if (body is null) return StatusCode(StatusCodes.Status413PayloadTooLarge);

        if (!SignatureMatches(configured, Request.Headers[configured.SignatureHeader].ToString(), body))
        {
            logger.LogWarning("Courier webhook for provider {Provider} refused: bad signature.", provider);
            return Unauthorized();
        }

        TrackingEventBody? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<TrackingEventBody>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "The body is not a tracking event." });
        }

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.EventId) || string.IsNullOrWhiteSpace(parsed.TrackingNumber) || string.IsNullOrWhiteSpace(parsed.Status) || parsed.OccurredOn == default)
            return BadRequest(new { error = "eventId, trackingNumber, status and occurredOn are required." });

        var response = await dispatcher.SendAsync(new ApplyCourierTrackingEventCommand
        {
            Provider = provider, EventId = parsed.EventId.Trim(), TrackingNumber = parsed.TrackingNumber.Trim(), Status = parsed.Status.Trim(), OccurredOn = parsed.OccurredOn.ToUniversalTime(),
            UserContext = new UserContext(SystemActors.CourierWebhook, Guid.Empty) { UserName = "courier-webhook" }, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return BadRequest(response.ValidationResult);

        logger.LogInformation("Courier webhook event accepted for provider {Provider}.", provider);
        return Ok(new { });
    }

    private static bool SignatureMatches(CourierWebhookOptions configured, string header, byte[] body)
    {
        var signature = header.Trim();
        if (signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) signature = signature["sha256=".Length..];

        var expected = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(configured.Secret), body));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(signature.ToLowerInvariant()), Encoding.ASCII.GetBytes(expected));
    }

    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await request.Body.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes) return null;
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private sealed record TrackingEventBody(string? EventId, string? TrackingNumber, string? Status, DateTimeOffset OccurredOn);
}
