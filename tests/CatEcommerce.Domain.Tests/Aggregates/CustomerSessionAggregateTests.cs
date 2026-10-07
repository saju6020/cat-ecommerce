using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates;

/// <summary>HO-ACC-001 §15 -- Sign-in & sessions (C). ARCHITECTURE gate fifth pass, LOW
/// (2026-09-25): RefreshCsrfToken gets the same ended-session guard
/// <see cref="StaffSessionAggregate.RecordActivity"/> already has -- see
/// StaffSessionAggregateTests's identical pair for the staff aggregate.</summary>
public sealed class CustomerSessionAggregateTests
{
    private static readonly DateTimeOffset IssuedOn = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Stamp = Guid.NewGuid();

    private static CustomerSessionAggregate Issued()
    {
        var session = new CustomerSessionAggregate(Guid.NewGuid());
        session.Issue(new SessionIssueDto(
            Guid.NewGuid(), "hash", Stamp, IssuedOn, IssuedOn.AddDays(7), null, null, TestUserContext.Create(), Guid.NewGuid(), "csrf-hash"));
        return session;
    }

    [Fact]
    public void RefreshCsrfToken_OnAnEndedSession_Throws()
    {
        var session = Issued();
        session.End(new SessionEndDto(IssuedOn.AddHours(1), SessionEndReason.SignedOut, TestUserContext.Create(), Guid.NewGuid()));

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
