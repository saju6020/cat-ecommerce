using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.Events.Operations;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Operations;

/// <summary>HO-XCT-001 U-B1..U-B4 -- the erasure ledger (section 8.3, ADR-302).</summary>
public sealed class ErasureRequestAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 12);

    private static ErasureRequestAggregate Open(params string[] keys)
    {
        var ledger = new ErasureRequestAggregate(Guid.NewGuid());
        ledger.Open(new ErasureOpenDto(Now, ErasureRequestSource.SelfService, keys, TestUserContext.Create(), Guid.NewGuid()));
        return ledger;
    }

    private static ErasureScopeOutcomeDto Outcome(string key, ErasureOutcome outcome) =>
        new(key, outcome, Now, Today, TestUserContext.Create(), Guid.NewGuid());

    // U-B1
    [Fact]
    public void U_B1_Open_CreatesOnePendingScopePerContext_AndRaisesAnEventWithIdsOnly()
    {
        var ledger = Open("account", "notification", "administration");

        Assert.Equal(3, ledger.Scopes.Count);
        Assert.All(ledger.Scopes, s => Assert.Equal(ErasureScopeStatus.Pending, s.Status));
        Assert.Equal(ErasureRequestStatus.Open, ledger.Status);

        var opened = Assert.IsType<ErasureRequestOpenedEvent>(Assert.Single(ledger.Events));
        Assert.Equal(ledger.Id, opened.CustomerAccountId);
        Assert.Equal(ErasureRequestSource.SelfService, opened.RequestSource);
    }

    [Theory]
    [InlineData]
    [InlineData("account", "account")]
    public void Open_NoContexts_OrDuplicateContexts_IsRefused(params string[] keys)
    {
        var ledger = new ErasureRequestAggregate(Guid.NewGuid());

        Assert.Throws<DomainRuleViolationException>(() =>
            ledger.Open(new ErasureOpenDto(Now, ErasureRequestSource.SelfService, keys, TestUserContext.Create(), Guid.NewGuid())));
    }

    // U-B2
    [Fact]
    public void U_B2_Held_WithADateInTheFuture_IsRecorded_ButTodayOrEarlierIsInvalidHoldDate()
    {
        var ledger = Open("administration", "account");

        ledger.RecordScopeOutcome(Outcome("administration", new ErasureOutcome.Held(Today.AddDays(1), "NFR-LEG-5 acceptance evidence")));

        var held = ledger.Scopes.Single(s => s.ContextKey == "administration");
        Assert.Equal(ErasureScopeStatus.Held, held.Status);
        Assert.Equal(Today.AddDays(1), held.HeldUntil);
        Assert.Equal("NFR-LEG-5 acceptance evidence", held.HeldBasis);

        var refused = Assert.Throws<DomainRuleViolationException>(() =>
            ledger.RecordScopeOutcome(Outcome("account", new ErasureOutcome.Held(Today, "x"))));
        Assert.Equal(BusinessViolationCodes.InvalidHoldDate, refused.ErrorCode);
        Assert.Equal(ErasureScopeStatus.Pending, ledger.Scopes.Single(s => s.ContextKey == "account").Status);
    }

    // U-B3
    [Fact]
    public void U_B3_TheLastScopeCompleting_CompletesTheRequest_AndRaisesTheCompletionEventOnce()
    {
        var ledger = Open("account", "notification");

        ledger.RecordScopeOutcome(Outcome("account", new ErasureOutcome.Completed()));
        Assert.Equal(ErasureRequestStatus.Open, ledger.Status);

        ledger.RecordScopeOutcome(Outcome("notification", new ErasureOutcome.Completed()));

        Assert.Equal(ErasureRequestStatus.Completed, ledger.Status);
        Assert.Equal(Now, ledger.CompletedOn);
        Assert.Single(ledger.Events.OfType<ErasureRequestCompletedEvent>());
    }

    [Fact]
    public void AFailedScope_IsFailing_CountsTheAttempt_AndKeepsOnlyACode()
    {
        var ledger = Open("account");

        ledger.RecordScopeOutcome(Outcome("account", new ErasureOutcome.Failed("UnhandledEraserException")));
        ledger.RecordScopeOutcome(Outcome("account", new ErasureOutcome.Failed("UnhandledEraserException")));

        var scope = ledger.Scopes.Single();
        Assert.Equal(ErasureScopeStatus.Failing, scope.Status);
        Assert.Equal(2, scope.Attempts);
        Assert.Equal("UnhandledEraserException", scope.LastErrorCode);
        Assert.Equal(ErasureRequestStatus.Open, ledger.Status);
        Assert.Empty(ledger.Events.OfType<ErasureRequestCompletedEvent>());
    }

    [Fact]
    public void ACompletedScope_CannotBeRecordedAgain()
    {
        var ledger = Open("account", "notification");
        ledger.RecordScopeOutcome(Outcome("account", new ErasureOutcome.Completed()));

        Assert.Throws<DomainRuleViolationException>(() => ledger.RecordScopeOutcome(Outcome("account", new ErasureOutcome.Completed())));
    }

    // U-B4
    [Fact]
    public void U_B4_AddMissingScopes_ReopensACompletedRequest_AndAddsAPendingScope()
    {
        var ledger = Open("account");
        ledger.RecordScopeOutcome(Outcome("account", new ErasureOutcome.Completed()));
        Assert.Equal(ErasureRequestStatus.Completed, ledger.Status);

        var changed = ledger.AddMissingScopes(new ErasureAddScopesDto(["account", "boarding"], TestUserContext.Create(), Guid.NewGuid()));

        Assert.True(changed);
        Assert.Equal(ErasureRequestStatus.Open, ledger.Status);
        Assert.Null(ledger.CompletedOn);
        Assert.Equal(ErasureScopeStatus.Pending, ledger.Scopes.Single(s => s.ContextKey == "boarding").Status);
        Assert.Equal(ErasureScopeStatus.Completed, ledger.Scopes.Single(s => s.ContextKey == "account").Status);
    }

    [Fact]
    public void AddMissingScopes_WithNothingNew_IsANoOp()
    {
        var ledger = Open("account");
        var before = ledger.Events.Count;

        Assert.False(ledger.AddMissingScopes(new ErasureAddScopesDto(["account"], TestUserContext.Create(), Guid.NewGuid())));
        Assert.Equal(before, ledger.Events.Count);
    }
}
