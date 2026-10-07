using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;

namespace CatEcommerce.Domain.Tests.Aggregates;

/// <summary>HO-ACC-001 §15 -- Staff sign-in & idle (G) / ADR-037.</summary>
public sealed class StaffSessionAggregateTests
{
    private static readonly DateTimeOffset IssuedOn = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Stamp = Guid.NewGuid();

    private static StaffSessionAggregate Issued()
    {
        var session = new StaffSessionAggregate(Guid.NewGuid());
        session.Issue(new SessionIssueDto(
            Guid.NewGuid(), "hash", Stamp, IssuedOn, IssuedOn.AddHours(12), null, null, TestUserContext.Create(), Guid.NewGuid(), "csrf-hash"));
        return session;
    }

    // U-G1
    [Fact]
    public void RecordActivity_Within60Seconds_DoesNotAdvance()
    {
        var session = Issued();
        session.RecordActivity(new StaffSessionActivityDto(IssuedOn.AddSeconds(30)));
        Assert.Equal(IssuedOn, session.LastActivityOn);
    }

    [Fact]
    public void RecordActivity_After60Seconds_Advances()
    {
        var session = Issued();
        session.RecordActivity(new StaffSessionActivityDto(IssuedOn.AddSeconds(61)));
        Assert.Equal(IssuedOn.AddSeconds(61), session.LastActivityOn);
    }

    // U-G2
    [Fact]
    public void EndForIdleTimeout_SetsEndedOnToActualExpiryMoment()
    {
        var session = Issued();
        var idleTimeout = TimeSpan.FromMinutes(30);
        var detectedOn = IssuedOn.Add(idleTimeout).AddMinutes(5);

        session.EndForIdleTimeout(new StaffSessionIdleTimeoutDto(detectedOn, idleTimeout, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(IssuedOn.Add(idleTimeout), session.EndedOn);
    }

    [Fact]
    public void EndForIdleTimeout_CalledTwice_Throws()
    {
        var session = Issued();
        var idleTimeout = TimeSpan.FromMinutes(30);
        var detectedOn = IssuedOn.Add(idleTimeout).AddMinutes(5);
        session.EndForIdleTimeout(new StaffSessionIdleTimeoutDto(detectedOn, idleTimeout, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Throws<DomainRuleViolationException>(() => session.EndForIdleTimeout(
            new StaffSessionIdleTimeoutDto(detectedOn.AddMinutes(1), idleTimeout, TestUserContext.Create(), Guid.NewGuid())));
    }

    // U-G3 -- terminal: even lengthening the configured idle period does not revive an ended session.
    [Fact]
    public void StateAt_AfterIdleTimeout_RemainsEndedRegardlessOfLongerConfiguredIdlePeriod()
    {
        var session = Issued();
        var idleTimeout = TimeSpan.FromMinutes(30);
        session.EndForIdleTimeout(new StaffSessionIdleTimeoutDto(
            IssuedOn.Add(idleTimeout).AddMinutes(5), idleTimeout, TestUserContext.Create(), Guid.NewGuid()));

        var state = session.StateAt(IssuedOn.AddHours(1), Stamp, TimeSpan.FromMinutes(120));

        Assert.Equal(CatEcommerce.Shared.Common.Enums.SessionState.Ended, state);
    }

    // U-G4
    [Fact]
    public void Aggregate_HasNoReviveExtendOrReactivateMethod()
    {
        var methodNames = typeof(StaffSessionAggregate).GetMethods().Select(m => m.Name);
        Assert.DoesNotContain(methodNames, n => n is "Revive" or "Extend" or "Reactivate");
    }

    // ARCHITECTURE gate fifth pass, LOW (2026-09-25): RefreshCsrfToken gets the same ended-session
    // guard RecordActivity already has.
    [Fact]
    public void RefreshCsrfToken_OnAnEndedSession_Throws()
    {
        var session = Issued();
        var idleTimeout = TimeSpan.FromMinutes(30);
        session.EndForIdleTimeout(new StaffSessionIdleTimeoutDto(
            IssuedOn.Add(idleTimeout).AddMinutes(5), idleTimeout, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Throws<DomainRuleViolationException>(() => session.RefreshCsrfToken("a-new-hash"));
    }

    [Fact]
    public void RefreshCsrfToken_OnAnActiveSession_UpdatesTheHash()
    {
        var session = Issued();
        session.RefreshCsrfToken("a-new-hash");
        Assert.Equal("a-new-hash", session.CsrfTokenHash);
    }
}
