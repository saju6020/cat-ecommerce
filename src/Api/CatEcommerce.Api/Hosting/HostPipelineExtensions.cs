// HO-ACC-001 ARCHITECTURE gate, fourth pass, MEDIUM (owner decision B, 2026-09-25): the host-wide
// CORS policy and the ForwardedHeadersOptions configuration used to live inside AccountsModule
// (an area module) even though Program.cs's shared pipeline block applies both ahead of every
// area, not just Accounts'. That is an ADR-341 violation -- an area module owning a host-level
// concern -- and it also meant the policy's name ("AccountsAllowedOrigins") implied an ownership
// that was never true. Both now live here, in shared host code, registered once from Program.cs's
// own shared block and owned by no area.
//
// HO-NTF-001 ARCHITECTURE gate fix (2026-09-27, carried from HO-ADM-001-ARCHITECTURE MEDIUM 1,
// integrator-authorised): the shared rate-limiter base (the 429 response shape, via `OnRejected`)
// and `UseRateLimiter()` itself used to live in AccountsModule too, even though Notifications is
// now a second area contributing its own `AddRateLimiter(...)` policies (which compose correctly
// through ASP.NET Core's `Configure<RateLimiterOptions>` either way) -- but depended on
// AccountsModule for the 429 body shape and for calling `UseRateLimiter()` at all. Both now live
// here; each area's own module adds only its own named policies.

using System.Net;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Validation;
using MetadataName = System.Threading.RateLimiting.MetadataName;

namespace CatEcommerce.Api.Hosting;

public static class HostPipelineExtensions
{
    /// <summary>The named CORS policy Program.cs's shared pipeline applies with
    /// <c>UseCors(HostPipelineExtensions.CorsPolicyName)</c>, replacing UseHttpPipeline()'s own
    /// allow-any-origin-with-credentials policy (SECURITY gate MEDIUM). Not owned by any area.</summary>
    public const string CorsPolicyName = "HostAllowedOrigins";

    /// <summary>
    /// Registers the two host-wide pipeline concerns Program.cs's shared block applies ahead of
    /// every area: the forwarded-headers trust configuration (§20.12, ADR-040) and the CORS
    /// allow-list (SECURITY gate MEDIUM). Call once, from Program.cs, before the per-area module
    /// registrations -- this is host composition, not a feature area (ADR-341).
    /// </summary>
    public static IServiceCollection AddHostPipelineServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RequestOriginOptions>(configuration.GetSection(RequestOriginOptions.SectionName));

        // Forwarded headers (§20.12, ADR-040) -- trusted proxies ONLY from configuration.
        // SECURITY gate HIGH (pass 1): ASP.NET Core's ForwardedHeadersMiddleware treats an EMPTY
        // KnownProxies/KnownNetworks pair as "skip the known-IP check", not "trust nothing" --
        // clearing both lists and leaving them empty meant X-Forwarded-For was honoured from ANY
        // peer, which defeats every per-IP rate limit and lets a caller write any IP it likes into
        // every audit row. Forwarded headers are now processed at all ONLY when at least one
        // trusted proxy or network is actually configured; with neither configured (the committed
        // appsettings.json), ForwardedHeaders is None and the middleware is a no-op, leaving
        // RemoteIpAddress as whatever the transport actually reports.
        //
        // Deployment note (ADR-342): do not set the ASPNETCORE_FORWARDEDHEADERS_ENABLED
        // environment variable on this host. That flag makes the framework itself call
        // UseForwardedHeaders() a second time with its own default options, which clear
        // KnownProxies/KnownNetworks and would reopen this HIGH regardless of what is configured
        // here. Trust a proxy only via RequestOrigin:TrustedProxies/TrustedNetworks.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            var requestOrigin = configuration.GetSection(RequestOriginOptions.SectionName).Get<RequestOriginOptions>() ?? new RequestOriginOptions();

            var trustedProxies = requestOrigin.TrustedProxies
                .Select(p => IPAddress.TryParse(p, out var address) ? address : null)
                .OfType<IPAddress>()
                .ToList();

            var trustedNetworks = requestOrigin.TrustedNetworks
                .Select(TryParseCidr)
                .OfType<Microsoft.AspNetCore.HttpOverrides.IPNetwork>()
                .ToList();

            if (trustedProxies.Count == 0 && trustedNetworks.Count == 0)
            {
                options.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();

            foreach (var proxy in trustedProxies) options.KnownProxies.Add(proxy);
            foreach (var network in trustedNetworks) options.KnownNetworks.Add(network);
        });

        // CORS (SECURITY gate MEDIUM) -- explicit origin allow-list from configuration. Replaces
        // UseHttpPipeline()'s own CORS policy, which reflected any origin
        // (SetIsOriginAllowed(_ => true)) together with AllowCredentials() -- exploitable once
        // cookie sessions exist, because SameSite=Lax still lets any same-site origin send them.
        // An empty AllowedOrigins list (the committed appsettings.json) means WithOrigins is never
        // called and the policy therefore grants no origin at all -- fail closed, the same "empty
        // means untrusted" convention as TrustedProxies/TrustedNetworks above.
        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        {
            var allowedOrigins = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()?.AllowedOrigins ?? [];
            if (allowedOrigins.Length == 0) return;

            // `Date` is exposed so a cross-origin web app can read the server clock: HO-APT-002's
            // hold countdown (ADR-FE-025) derives its offset from it (backlog APT row W1).
            policy.WithOrigins(allowedOrigins)
                .AllowCredentials()
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders("Date");
        }));

        // Rate limiting (§12.7, ADR-030) -- the shared base only. Composes with each area's own
        // `AddRateLimiter(...)` call (Accounts', Notifications') through ASP.NET Core's
        // `Configure<RateLimiterOptions>` -- every call's policies are added to the same options
        // instance regardless of call order. Only the 429 response shape lives here, once.
        services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, ct) =>
            {
                var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? (int)retryAfter.TotalSeconds
                    : 60;

                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                context.HttpContext.Response.StatusCode = 429;

                var validationResponse = new ValidationResponse();
                validationResponse.AddError(
                    errorMessage: "Too many requests. Try again later.", propertyName: "Request",
                    errorCode: BusinessViolationCodes.TooManyRequests, resourceName: "RateLimit");

                await context.HttpContext.Response.WriteAsJsonAsync(validationResponse, ct);
            };
        });

        return services;
    }

    /// <summary>Parses a "network/prefixLength" CIDR string (e.g. "10.0.0.0/8"). Returns
    /// <c>null</c> for anything malformed, rather than throwing at startup on an operator typo.</summary>
    private static Microsoft.AspNetCore.HttpOverrides.IPNetwork? TryParseCidr(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2) return null;
        if (!IPAddress.TryParse(parts[0], out var address)) return null;
        if (!int.TryParse(parts[1], out var prefixLength)) return null;

        try
        {
            return new Microsoft.AspNetCore.HttpOverrides.IPNetwork(address, prefixLength);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
