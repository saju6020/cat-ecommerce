using System.Text.Json;

namespace CatEcommerce.Api.Hosting;

/// <summary>
/// ADR-342 (rewritten twice -- second ARCHITECTURE gate pass, then the SECURITY gate). The ONE
/// exception boundary for this entire application, registered exactly once in
/// <c>Program.cs</c>'s shared pipeline prefix (<c>UseForwardedHeaders</c> -&gt;
/// <c>UseRouting</c> -&gt; this middleware -&gt; <c>UseCors</c> -&gt; each area's own middleware
/// -&gt; <c>MapHealthChecks</c>/<c>MapControllers</c>).
///
/// <b>Why this exists instead of the platform's own <c>GlobalExceptionHandlerMiddleware</c>, and
/// why this host no longer calls <c>UseHttpPipeline()</c> at all.</b> Decompiled
/// (<c>Platform.Infrastructure.Host.WebApi</c> 1.0.14), that middleware calls
/// <c>context.Request.Body</c> to the end and logs it verbatim ("Payload: {body}", Error level)
/// for any <c>POST</c> whose <c>Content-Type</c> is exactly <c>application/json</c> -- what
/// <c>web/lib/api/client.ts</c> sends on every request -- and also logs the full request URL
/// including its query string. The ARCHITECTURE second pass placed this middleware ahead of
/// <c>UseHttpPipeline()</c>, which stopped the leak for exceptions thrown in the session
/// middleware and the rate limiter, but the platform handler still ran *inside*
/// <c>UseHttpPipeline()</c>, nested around <c>UseCors</c>/<c>UseEndpoints</c> -- so it still caught,
/// and could still log the body of, any exception MVC itself throws before model binding (a
/// resource or authorization filter, controller activation) on a password-bearing endpoint
/// (SECURITY gate LOW, confirmed live with a throwing resource filter on sign-in). Dropping
/// <c>UseHttpPipeline()</c> entirely -- replacing its CORS policy with an explicit,
/// configuration-driven one and its health-check mapping with an explicit
/// <c>MapHealthChecks("/health")</c> call, both in <c>Program.cs</c> -- removes that inner instance.
/// This middleware is now the ONLY exception handler anywhere in the pipeline, for exceptions from
/// ANY position: the session middleware, the rate limiter, MVC filters, controller activation, or
/// a controller action itself. A database fault or timeout during session resolution
/// (<see cref="CatEcommerce.Api.Sessions.SessionAuthenticationMiddleware"/> dispatches
/// <c>GetCurrentCustomerSessionQuery</c>/<c>GetStaffSessionStatusQuery</c>/
/// <c>RecordStaffSessionActivityCommand</c>, and <c>Platform.Infrastructure.Core</c>'s
/// <c>QueryProcessor</c> catches nothing) can therefore never log the caller's plaintext password
/// or reset token -- sign-in, change-password, the account-deletion step-up, staff sign-in and
/// reset confirmation all carry one (HO-ACC-001 §4.4, §20.2: an explicitly forbidden shape, "no
/// plaintext password, token or CAPTCHA answer in any ... log line").
///
/// This middleware never reads <see cref="HttpContext.Request"/> at all beyond
/// <see cref="HttpRequest.Method"/> and <see cref="HttpRequest.Path"/> for the log line -- never
/// the body, a header value or the query string, any of which can carry a secret HO-ACC-001 §4.4
/// forbids logging. It matches the platform handler's response shape for the configured
/// <c>ErrorVerbosity</c>, but the request payload can never reach it: with <c>ErrorVerbosity</c>
/// unset (the committed <c>appsettings.json</c>), the response is an empty-body <c>500</c>,
/// identical to what the platform handler already produces for that configuration. The
/// <c>StackTrace</c> case reproduces the platform's own (internal, inaccessible)
/// <c>SerializableExceptionWrapper</c> shape field-for-field (<c>Message</c>, <c>StackTrace</c>,
/// <c>HResult</c>, <c>TargetSite</c>, <c>HelpLink</c>) without reusing the type itself, still
/// without ever touching the request.
/// </summary>
public sealed class SafeExceptionHandlerMiddleware(
    RequestDelegate next,
    IConfiguration configuration,
    ILogger<SafeExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            // Deliberately: method and path only. Never the body, a header or the query string.
            logger.LogError(exception, "Unhandled exception on [{Method}] {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted) return;

            context.Response.Clear();
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            switch (configuration["ErrorVerbosity"])
            {
                case "None":
                    await JsonSerializer.SerializeAsync(context.Response.Body, "An error was encountered");
                    break;
                case "ExceptionMessage":
                    await JsonSerializer.SerializeAsync(context.Response.Body, exception.Message);
                    break;
                case "StackTrace":
                    // Field-for-field match of the platform's own (internal) SerializableExceptionWrapper.
                    await JsonSerializer.SerializeAsync(context.Response.Body, new
                    {
                        Message = exception.Message,
                        StackTrace = exception.StackTrace,
                        HResult = exception.HResult,
                        TargetSite = "Method : " + exception.TargetSite?.Name + " Class: " + exception.TargetSite?.DeclaringType?.AssemblyQualifiedName,
                        HelpLink = exception.HelpLink
                    });
                    break;
                // Unset (the committed appsettings.json): no case matches, so nothing is written --
                // an empty body, exactly like the platform handler's own behaviour for this config.
            }
        }
    }
}
