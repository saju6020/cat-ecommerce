using CatEcommerce.Api.Authorization;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using CatEcommerce.Shared.SharedDto.Account;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Sessions;

/// <summary>
/// HO-ACC-001 §12.5 / ADR-031 / ADR-032. Placed after <c>UseForwardedHeaders</c> and routing,
/// before the rate limiter and <c>MapControllers</c> (I-C9, I-A7 -- the integrator adds the one
/// line; see <c>AccountsModule.UseAccountsModule</c>).
///
/// CSRF (ADR-342, rewritten -- owner decision A, 2026-09-25): the value compared against the
/// <c>X-CSRF-Token</c> header is a per-session random token's hash, stored on the session row
/// itself (<c>CustomerSessionViewModel.CsrfTokenHash</c> / <c>StaffSessionStatusViewModel.CsrfTokenHash</c>),
/// not derived from a process-lifetime key -- it is therefore durable across a process restart
/// exactly like the session it protects (SECURITY gate HIGH, second pass). The <c>pc_csrf</c>
/// cookie's own value is never trusted as proof of anything; on a safe request it is read only to
/// decide whether to opportunistically re-issue a fresh one.
/// </summary>
public sealed class SessionAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IInMemoryDispatcher dispatcher,
        ISessionTokenIssuer sessionTokenIssuer,
        ISessionPolicy sessionPolicy,
        IClientIpAccessor clientIpAccessor,
        IUserContextProvider userContextProvider,
        TimeProvider timeProvider,
        SessionCookieWriter cookieWriter,
        IServiceScopeFactory scopeFactory)
    {
        var asOf = timeProvider.GetUtcNow();
        var caller = ResolvedCaller.Anonymous;
        ResolvedSession? resolvedSession = null;

        // ADR-342 (owner decision A): sign-in must never be blocked by a stale or otherwise
        // unresolvable session cookie of its OWN kind, let alone by CSRF -- re-authenticating IS
        // how a legitimate caller recovers from exactly that situation. [CsrfExempt] marks both
        // sign-in actions; nothing else on this host carries it. Session resolution itself
        // (`TryResolveStaffAsync`'s R5 hard-refusal, and its idle-timeout/activity side effects) is
        // therefore skipped entirely for these two endpoints -- they run as anonymous regardless of
        // what either session cookie contains, matching the customer branch's own "unresolvable
        // means anonymous" behaviour below, which staff resolution does not share by design for
        // every OTHER staff endpoint.
        var isCsrfExempt = context.GetEndpoint()?.Metadata.GetMetadata<CsrfExemptAttribute>() is not null;

        var staffToken = context.Request.Cookies[SessionCookieWriter.StaffSessionCookie];
        var customerToken = context.Request.Cookies[SessionCookieWriter.CustomerSessionCookie];

        if (isCsrfExempt)
        {
            // Deliberately does nothing with either cookie here: a failed sign-in attempt must not
            // have the side effect of ending a still-valid pre-existing session, and a successful
            // one has the controller overwrite both session cookies anyway (AuthController.SignIn /
            // StaffAuthController.SignIn).
        }
        else if (!string.IsNullOrEmpty(staffToken))
        {
            var resolved = await TryResolveStaffAsync(
                context, staffToken, asOf, dispatcher, sessionTokenIssuer, sessionPolicy,
                clientIpAccessor, userContextProvider, cookieWriter, scopeFactory);

            if (resolved is null) return; // response already written (R5)
            (caller, resolvedSession) = resolved.Value;
        }
        else if (!string.IsNullOrEmpty(customerToken))
        {
            var tokenHash = sessionTokenIssuer.HashToken(customerToken);
            var response = await dispatcher.GetResultAsync(new GetCurrentCustomerSessionQuery
            {
                TokenHash = tokenHash,
                AsOf = asOf,
                UserContext = userContextProvider.GetUserContext(),
                CorrelationId = Guid.NewGuid()
            });

            if (response.Result is not null)
            {
                caller = ResolvedCaller.Customer(response.Result.CustomerId);
                resolvedSession = new ResolvedSession(tokenHash, response.Result.CsrfTokenHash, response.Result.ExpiresOn, IsStaff: false);
            }
            else
                cookieWriter.ExpireCustomerSession(context); // treated as anonymous -- a guest page must still render
        }

        if (!isCsrfExempt && caller.Kind != ResolvedCallerKind.Anonymous)
        {
            if (IsUnsafeMethod(context.Request.Method))
            {
                var csrfHeader = context.Request.Headers[SessionCookieWriter.CsrfHeader].FirstOrDefault();
                var presentedHash = string.IsNullOrEmpty(csrfHeader) ? null : sessionTokenIssuer.HashToken(csrfHeader);

                if (ShouldRefuseUnsafeRequest(caller.Kind, resolvedSession, presentedHash))
                {
                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new { error = "csrf_token_invalid" });
                    return;
                }
            }
            else if (resolvedSession is not null)
            {
                // Safe request: opportunistically re-issue pc_csrf when it is missing or stale, so
                // a legitimate browser recovers on its own instead of being stuck for the life of
                // the session (SECURITY gate HIGH, second pass). The cookie's own value is read
                // here only to decide whether to refresh it -- it is never used to authorize
                // anything. Nothing to reissue against without a resolved session -- silently
                // continuing there has no security effect (there is no CSRF check to bypass on a
                // safe method), unlike the unsafe-method branch above.
                var csrfCookie = context.Request.Cookies[SessionCookieWriter.CsrfCookie];
                var isStale = string.IsNullOrEmpty(csrfCookie) ||
                              !ConstantTimeHashEquals(sessionTokenIssuer, csrfCookie, resolvedSession.CsrfTokenHash);

                if (isStale)
                    await TryReissueCsrfAsync(context, dispatcher, cookieWriter, resolvedSession);
            }
        }

        context.Items[ResolvedCaller.HttpContextItemsKey] = caller;
        await next(context);
    }

    private static bool ConstantTimeHashEquals(ISessionTokenIssuer sessionTokenIssuer, string presentedToken, string expectedHash) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(sessionTokenIssuer.HashToken(presentedToken)),
            System.Text.Encoding.UTF8.GetBytes(expectedHash));

    /// <summary>
    /// ARCHITECTURE gate fifth pass, LOW (2026-09-25): the pure CSRF decision for an unsafe method,
    /// factored out of <see cref="InvokeAsync"/> so it is directly unit-testable, and so its shape
    /// cannot silently regress into failing OPEN. Fails closed (refuses) whenever
    /// <paramref name="resolvedSession"/> is <c>null</c> for a non-anonymous caller -- both call
    /// sites in <see cref="InvokeAsync"/> always set <paramref name="callerKind"/> and
    /// <paramref name="resolvedSession"/> together today, so that combination is currently
    /// unreachable, but this method must never silently skip the check if a future caller source
    /// ever sets one without the other. <paramref name="presentedCsrfHeaderHash"/> is the ALREADY
    /// hashed <c>X-CSRF-Token</c> header value (or <c>null</c> if the header was missing or empty) --
    /// hashing itself needs <see cref="ISessionTokenIssuer"/> and stays in <see cref="InvokeAsync"/>,
    /// so this method itself needs no dependency to be tested.
    /// </summary>
    internal static bool ShouldRefuseUnsafeRequest(ResolvedCallerKind callerKind, ResolvedSession? resolvedSession, string? presentedCsrfHeaderHash)
    {
        if (callerKind == ResolvedCallerKind.Anonymous) return false; // nothing to protect

        if (resolvedSession is null) return true; // fail CLOSED -- see summary above

        return string.IsNullOrEmpty(presentedCsrfHeaderHash) ||
               !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                   System.Text.Encoding.UTF8.GetBytes(presentedCsrfHeaderHash),
                   System.Text.Encoding.UTF8.GetBytes(resolvedSession.CsrfTokenHash));
    }

    /// <summary>Best-effort: a refusal or a concurrency conflict here must never block the
    /// underlying safe request, which is why this does not use <c>DispatchWithOneRetryAsync</c> --
    /// the reissuance is opportunistic, the request behind it is not.</summary>
    private static async Task TryReissueCsrfAsync(
        HttpContext context, IInMemoryDispatcher dispatcher, SessionCookieWriter cookieWriter, ResolvedSession resolvedSession)
    {
        try
        {
            var response = resolvedSession.IsStaff
                ? await dispatcher.SendAsync(new RefreshStaffSessionCsrfTokenCommand { TokenHash = resolvedSession.TokenHash, ValidateCommand = true })
                : await dispatcher.SendAsync(new RefreshCustomerSessionCsrfTokenCommand { TokenHash = resolvedSession.TokenHash, ValidateCommand = true });

            if (response.IsSuccess && response.Result is CsrfRefreshResult refreshed)
                cookieWriter.WriteCsrf(context, refreshed.Token, resolvedSession.ExpiresOn);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request refreshed (or ended) this session first -- leave the client's
            // existing cookie alone; the next safe request will re-evaluate.
        }
    }

    /// <summary>Returns the resolved staff caller and its session details, or <c>null</c> if a
    /// refusal (R5) was already written to the response and the pipeline must stop here.</summary>
    private static async Task<(ResolvedCaller Caller, ResolvedSession Session)?> TryResolveStaffAsync(
        HttpContext context, string staffToken, DateTimeOffset asOf, IInMemoryDispatcher dispatcher,
        ISessionTokenIssuer sessionTokenIssuer, ISessionPolicy sessionPolicy, IClientIpAccessor clientIpAccessor,
        IUserContextProvider userContextProvider, SessionCookieWriter cookieWriter, IServiceScopeFactory scopeFactory)
    {
        var tokenHash = sessionTokenIssuer.HashToken(staffToken);

        var statusResponse = await dispatcher.GetResultAsync(new GetStaffSessionStatusQuery
        {
            TokenHash = tokenHash,
            AsOf = asOf,
            IdleTimeoutMinutes = (int)sessionPolicy.StaffIdleTimeout.TotalMinutes,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        var status = statusResponse.Result;
        if (status is null)
        {
            cookieWriter.ExpireStaffSession(context);
            await WriteRefusalAsync(context, "session_invalid", null);
            return null;
        }

        var session = new ResolvedSession(tokenHash, status.CsrfTokenHash, status.AbsoluteExpiresOn, IsStaff: true);

        var isPassive = context.GetEndpoint()?.Metadata.GetMetadata<PassiveSessionAttribute>() is not null;
        if (isPassive)
        {
            // §8.4 / ARCHITECTURE gate: apply the same SessionStateRules.Classify the aggregate
            // and the non-passive path use, so the read side cannot disagree about idle state.
            // Q-G1's own SQL already guarantees EndedOn is null, ExpiresOn > asOf and the security
            // stamp matches (that is what "status is not null" above means), so the stamp
            // arguments below are passed as an equal pair purely to reach Classify's idle check --
            // they cannot themselves change the outcome. [PassiveSession] must not extend the
            // idle window (ADR-037), so this never dispatches RecordStaffSessionActivityCommand;
            // it only refuses, exactly like every other staff endpoint (R5).
            var state = SessionStateRules.Classify(
                isEnded: false,
                expiresOn: status.AbsoluteExpiresOn,
                asOf: asOf,
                issuedForSecurityStamp: Guid.Empty,
                currentStamp: Guid.Empty,
                lastActivityOn: status.LastActivityOn,
                idleTimeout: sessionPolicy.StaffIdleTimeout);

            if (state == SessionState.IdleExpired)
            {
                // ADR-037: idle expiry must be made terminal and audited once on DETECTION, even
                // on a [PassiveSession] endpoint -- "never extends" means never advancing
                // LastActivityOn on an otherwise-active session, not "never ends an already-idle
                // one" (ARCHITECTURE gate, second pass, LOW). Dispatching the same C14 command the
                // non-passive path uses is safe here: its handler re-derives state from the
                // aggregate itself and, finding it idle-expired, calls EndForIdleTimeout -- never
                // RecordActivity -- so this cannot extend the session even though it is the same
                // command. The response below is authoritative regardless of this command's own
                // outcome (idempotent if something else already ended the session first).
                var idleTimeoutCommand = new RecordStaffSessionActivityCommand
                {
                    TokenHash = tokenHash,
                    IpAddress = clientIpAccessor.GetClientIpAddress()?.ToString(),
                    UserContext = userContextProvider.GetUserContext(),
                    CorrelationId = Guid.NewGuid(),
                    ValidateCommand = true
                };

                await DispatchWithOneRetryAsync(scopeFactory, idleTimeoutCommand);

                cookieWriter.ExpireStaffSession(context);
                await WriteRefusalAsync(context, "session_idle_timeout", (int)sessionPolicy.StaffIdleTimeout.TotalMinutes);
                return null;
            }

            return (ResolvedCaller.Staff(status.StaffAccountId, status.RoleIds), session);
        }

        // Dispatched in its own DI scope, one retry on DbUpdateConcurrencyException (§11.4 C14).
        // Each attempt gets a FRESH scope (and therefore a fresh, untracked DbContext) -- reusing
        // the same scope for the retry would still be holding the stale, conflicting
        // StaffSessionAggregate the first attempt's DbContext already has tracked, so the retry
        // would very likely conflict again instead of running against current state.
        var activityCommand = new RecordStaffSessionActivityCommand
        {
            TokenHash = tokenHash,
            IpAddress = clientIpAccessor.GetClientIpAddress()?.ToString(),
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var activityResponse = await DispatchWithOneRetryAsync(scopeFactory, activityCommand);

        if (!activityResponse.IsSuccess)
        {
            var errorCode = activityResponse.ValidationResult?.Errors.FirstOrDefault()?.ErrorCode;
            cookieWriter.ExpireStaffSession(context);

            if (errorCode == BusinessViolationCodes.SessionIdleTimeout)
                await WriteRefusalAsync(context, "session_idle_timeout", (int)sessionPolicy.StaffIdleTimeout.TotalMinutes);
            else
                await WriteRefusalAsync(context, "session_invalid", null);

            return null;
        }

        return (ResolvedCaller.Staff(status.StaffAccountId, status.RoleIds), session);
    }

    private static async Task<Platform.Infrastructure.Core.Commands.CommandResponse> DispatchWithOneRetryAsync(
        IServiceScopeFactory scopeFactory, RecordStaffSessionActivityCommand command)
    {
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            try
            {
                return await scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>().SendAsync(command);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Fall through to a fresh scope below -- see the caller's comment.
            }
        }

        await using var retryScope = scopeFactory.CreateAsyncScope();
        return await retryScope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>().SendAsync(command);
    }

    private static async Task WriteRefusalAsync(HttpContext context, string error, int? idleTimeoutMinutes)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.StatusCode = 401;

        if (idleTimeoutMinutes is null)
            await context.Response.WriteAsJsonAsync(new { error });
        else
            await context.Response.WriteAsJsonAsync(new { error, idleTimeoutMinutes });
    }

    private static bool IsUnsafeMethod(string method) =>
        method is "POST" or "PUT" or "DELETE" or "PATCH";

    /// <summary>The resolved session's identity for CSRF purposes -- carried alongside
    /// <see cref="ResolvedCaller"/> so the CSRF check and the reissuance path do not need to
    /// re-derive which kind of session (and therefore which refresh command) applies. Internal, not
    /// private, so <see cref="ShouldRefuseUnsafeRequest"/> is directly unit-testable from
    /// <c>CatEcommerce.IntegrationTests</c> (§16.1 -- that project already references this
    /// assembly).</summary>
    internal sealed record ResolvedSession(string TokenHash, string CsrfTokenHash, DateTimeOffset ExpiresOn, bool IsStaff);
}
