using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// Keeps a factory that runs on a <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/>
/// usable when the fake clock is BEHIND the wall clock.
///
/// Root cause it works around (test infrastructure, not a production bug): the API correctly writes
/// session cookies with <c>Expires = fakeNow + lifetime</c> (SessionCookieWriter), but the test
/// client's <c>CookieContainer</c> judges expiry against the REAL clock. With a fake "now" more than
/// a session lifetime in the past, every cookie arrives already expired, is silently dropped, and the
/// next request is anonymous / rejected as <c>{"error":"session_invalid"}</c>. Tests pinned to a fixed
/// calendar date (e.g. 2026-10-12) therefore start failing once the real date passes it.
///
/// Deletion cookies (Expires = Unix epoch) are left alone so sign-out still removes the cookie.
/// Server-side session expiry is unaffected: it is decided from the DB and the fake TimeProvider.
/// </summary>
internal static partial class RealClockCookieExpiry
{
    public static void Apply(IServiceCollection services) =>
        services.AddTransient<IStartupFilter, Filter>();

    [GeneratedRegex(@"expires=([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ExpiresPattern();

    private sealed class Filter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Response.OnStarting(() =>
                {
                    var headers = context.Response.Headers.SetCookie;
                    if (headers.Count == 0) return Task.CompletedTask;

                    var farFuture = DateTimeOffset.UtcNow.AddYears(1).ToString("R");
                    context.Response.Headers.SetCookie = headers.Select(h => h is null ? h : ExpiresPattern().Replace(h, m =>
                        DateTimeOffset.TryParse(m.Groups[1].Value, out var at) && at.Year > 1971
                            ? "expires=" + farFuture
                            : m.Value)).ToArray()!;
                    return Task.CompletedTask;
                });
                await nextMiddleware();
            });
            next(app);
        };
    }
}
