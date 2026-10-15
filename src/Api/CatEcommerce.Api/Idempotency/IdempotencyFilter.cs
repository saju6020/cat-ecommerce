using System.Security.Cryptography;
using System.Text;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Idempotency;

/// <summary>
/// HO-ORD-001 §8.13 (ADR-202), US-ORD-18 AC1 -- the HTTP idempotency resource filter behind <see cref="IdempotentAttribute"/>.
/// <list type="number">
/// <item><c>Idempotency-Key</c> must be a UUID, else <c>400 IdempotencyKeyRequired</c>.</item>
/// <item>Scope is caller kind + caller id + route template; the request hash covers method, path, query and the canonical JSON body.</item>
/// <item>The claim is committed on its own (<see cref="IIdempotencyStore"/>, a separate short-lived context) <b>before</b> the action runs.</item>
/// <item>A repeat with the same hash replays the stored status and body (<c>Idempotent-Replayed: true</c>), or is <c>409 RequestInProgress</c> while
/// the first is still running; a different hash is <c>422 IdempotencyKeyReused</c>.</item>
/// <item>After the action a 2xx or 4xx result is stored (body up to 64 KB, else the status alone); an unhandled exception or a 5xx releases the key so
/// a retry can run. The response is buffered so the record is <b>complete before the client sees it</b> -- an immediate retry replays.</item>
/// </list>
/// The response is never logged; the body is neither read from nor written to a log.
/// </summary>
public sealed class IdempotencyFilter(IIdempotencyStore store, TimeProvider timeProvider, ILogger<IdempotencyFilter> logger) : IAsyncResourceFilter
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);
    private const int MaxRequestBytes = 1_048_576;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var ct = http.RequestAborted;

        if (!http.Request.Headers.TryGetValue(IdempotencyRules.HeaderName, out var header) || !Guid.TryParse(header.ToString(), out var key) || key == Guid.Empty)
        {
            context.Result = Refusal(StatusCodes.Status400BadRequest, BusinessViolationCodes.IdempotencyKeyRequired, OrderMessages.IdempotencyKeyRequired);
            return;
        }

        var caller = http.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller ?? ResolvedCaller.Anonymous;
        var template = (context.ActionDescriptor as ControllerActionDescriptor)?.AttributeRouteInfo?.Template ?? http.Request.Path.Value ?? string.Empty;
        var scope = $"{caller.Kind}:{caller.AccountId:N}:{template}";
        if (scope.Length > IdempotencyRules.MaxScopeLength) scope = scope[..IdempotencyRules.MaxScopeLength];

        var hash = await HashRequestAsync(http.Request, ct);
        var claim = await store.ClaimAsync(scope, key, hash, timeProvider.GetUtcNow(), StaleAfter, ct);

        switch (claim.Outcome)
        {
            case IdempotencyClaimOutcome.Replay:
                logger.LogInformation("Replaying an idempotent response for key {IdempotencyKey}.", key);
                context.Result = new ReplayResult(claim.Replay!);
                return;

            case IdempotencyClaimOutcome.InProgress:
                context.Result = Refusal(StatusCodes.Status409Conflict, BusinessViolationCodes.RequestInProgress, OrderMessages.RequestInProgress);
                return;

            case IdempotencyClaimOutcome.KeyReused:
                context.Result = Refusal(StatusCodes.Status422UnprocessableEntity, BusinessViolationCodes.IdempotencyKeyReused, OrderMessages.IdempotencyKeyReused);
                return;
        }

        var original = http.Response.Body;
        await using var buffer = new MemoryStream();
        http.Response.Body = buffer;

        ResourceExecutedContext executed;
        try
        {
            executed = await next();
        }
        catch
        {
            http.Response.Body = original;
            await store.ReleaseAsync(scope, key, CancellationToken.None);
            throw;
        }

        http.Response.Body = original;

        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            await store.ReleaseAsync(scope, key, CancellationToken.None);
            return; // the exception continues to the host's exception boundary; nothing was written to the client
        }

        var status = http.Response.StatusCode;
        if (status is >= 200 and < 500)
        {
            var body = buffer.Length <= IdempotencyRules.MaxStoredBodyBytes ? buffer.ToArray() : null;
            await store.CompleteAsync(scope, key, new IdempotentResponse(status, http.Response.ContentType, body), timeProvider.GetUtcNow(), CancellationToken.None);
        }
        else
        {
            await store.ReleaseAsync(scope, key, CancellationToken.None);
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(original, CancellationToken.None);
    }

    private static ObjectResult Refusal(int status, string code, string message)
    {
        var validation = new ValidationResponse();
        validation.AddError(errorMessage: message, propertyName: IdempotencyRules.HeaderName, errorCode: code, resourceName: "Idempotency");
        return new ObjectResult(validation) { StatusCode = status };
    }

    private static async Task<string> HashRequestAsync(HttpRequest request, CancellationToken ct)
    {
        byte[] body = [];
        if (request.ContentLength is > 0 || request.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.EnableBuffering();
            using var raw = new MemoryStream();
            await request.Body.CopyToAsync(raw, ct);
            request.Body.Position = 0;
            body = CanonicalJson.Canonicalise(raw.ToArray());
        }

        var query = string.Join('&', request.Query.OrderBy(q => q.Key, StringComparer.Ordinal).Select(q => $"{q.Key}={q.Value}"));
        var head = Encoding.UTF8.GetBytes($"{request.Method}\n{request.Path.Value}\n{query}\n");

        return Convert.ToHexStringLower(SHA256.HashData([.. head, .. body]));
    }

    /// <summary>Writes the stored status, content type and body of the first request, marked as a replay.</summary>
    private sealed class ReplayResult(IdempotentResponse stored) : IActionResult
    {
        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = stored.Status;
            response.Headers[IdempotencyRules.ReplayedHeaderName] = "true";
            if (stored.ContentType is not null) response.ContentType = stored.ContentType;
            if (stored.Body is { Length: > 0 } body) await response.Body.WriteAsync(body);
        }
    }
}
