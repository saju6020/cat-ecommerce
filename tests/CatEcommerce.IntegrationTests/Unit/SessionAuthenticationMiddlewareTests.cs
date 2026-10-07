using CatEcommerce.Api.Sessions;
using CatEcommerce.Infrastructure.Hosting;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>
/// ARCHITECTURE gate fifth pass, LOW (2026-09-25): <c>ShouldRefuseUnsafeRequest</c> is the pure CSRF
/// decision for an unsafe method, factored out of <c>SessionAuthenticationMiddleware.InvokeAsync</c>
/// specifically so this fail-closed shape can be pinned down directly, without needing a live HTTP
/// scenario to reach it -- both call sites in <c>InvokeAsync</c> always set the caller and the
/// resolved session together today, so the "non-anonymous caller, no resolved session" combination
/// this test exercises is not reachable through the public API. Placed here per §15's routing rule
/// (exercises an Api class, not a database).
/// </summary>
public sealed class SessionAuthenticationMiddlewareTests
{
    private static SessionAuthenticationMiddleware.ResolvedSession Session(string csrfTokenHash) =>
        new(TokenHash: "session-token-hash", CsrfTokenHash: csrfTokenHash, ExpiresOn: DateTimeOffset.UtcNow.AddHours(1), IsStaff: false);

    [Fact]
    public void AnonymousCaller_NeverRefused_RegardlessOfSessionOrHeader()
    {
        Assert.False(SessionAuthenticationMiddleware.ShouldRefuseUnsafeRequest(
            ResolvedCallerKind.Anonymous, resolvedSession: null, presentedCsrfHeaderHash: null));

        Assert.False(SessionAuthenticationMiddleware.ShouldRefuseUnsafeRequest(
            ResolvedCallerKind.Anonymous, Session("some-hash"), presentedCsrfHeaderHash: "wrong-hash"));
    }

    // The fix this test pins down: before it, the caller in InvokeAsync required
    // `resolvedSession is not null` to even enter the CSRF branch, so a hypothetical future caller
    // source that set a non-anonymous caller WITHOUT a resolved session would have skipped the CSRF
    // check entirely (fail OPEN). This must refuse instead (fail CLOSED).
    [Theory]
    [InlineData(ResolvedCallerKind.Customer)]
    [InlineData(ResolvedCallerKind.Staff)]
    public void NonAnonymousCaller_WithNoResolvedSession_FailsClosed(ResolvedCallerKind callerKind)
    {
        var refused = SessionAuthenticationMiddleware.ShouldRefuseUnsafeRequest(
            callerKind, resolvedSession: null, presentedCsrfHeaderHash: "anything-at-all");

        Assert.True(refused);
    }

    [Fact]
    public void NonAnonymousCaller_MissingHeader_IsRefused()
    {
        var refused = SessionAuthenticationMiddleware.ShouldRefuseUnsafeRequest(
            ResolvedCallerKind.Customer, Session("stored-hash"), presentedCsrfHeaderHash: null);

        Assert.True(refused);
    }

    [Fact]
    public void NonAnonymousCaller_MismatchedHeader_IsRefused()
    {
        var refused = SessionAuthenticationMiddleware.ShouldRefuseUnsafeRequest(
            ResolvedCallerKind.Customer, Session("stored-hash"), presentedCsrfHeaderHash: "a-different-hash");

        Assert.True(refused);
    }

    [Fact]
    public void NonAnonymousCaller_MatchingHeader_IsAccepted()
    {
        var refused = SessionAuthenticationMiddleware.ShouldRefuseUnsafeRequest(
            ResolvedCallerKind.Customer, Session("stored-hash"), presentedCsrfHeaderHash: "stored-hash");

        Assert.False(refused);
    }
}
