using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.Aggregates.Notification;

public sealed class NotificationRequestAggregateTests
{
    private static NotificationRequestAggregate Create(IReadOnlyDictionary<string, string>? variables = null, string? dedupKey = null) =>
        Build(variables, dedupKey);

    private static NotificationRequestAggregate Build(IReadOnlyDictionary<string, string>? variables, string? dedupKey)
    {
        var aggregate = new NotificationRequestAggregate(Guid.NewGuid());
        aggregate.Create(new NotificationRequestCreateDto(
            "care.vaccination-due", NotificationRecipientKind.Customer, Guid.NewGuid(),
            variables ?? new Dictionary<string, string> { ["catName"] = "Mitu" },
            null, null, null, null, null, null, dedupKey, null, false,
            DateTimeOffset.UtcNow, Guid.NewGuid(), TestUserContext.Create(), Guid.NewGuid()));
        return aggregate;
    }

    [Fact]
    public void Create_TooManyVariables_Throws()
    {
        var variables = Enumerable.Range(0, NotificationRequestRules.MaxVariables + 1).ToDictionary(i => $"v{i}", i => "x");
        var ex = Assert.Throws<DomainRuleViolationException>(() => Create(variables));
        Assert.Equal(BusinessViolationCodes.InvalidNotificationRequest, ex.ErrorCode);
    }

    /// <summary>HO-NTF-001 SECURITY/CODE_REVIEW gate fix (2026-09-27, MEDIUM 7): defense in depth
    /// alongside TemplateRules' own template-side rejection.</summary>
    [Theory]
    [InlineData("Mitu")]
    [InlineData("Mitu\0")]
    [InlineData("Mitu​")]
    public void Create_VariableWithDisallowedCharacter_Throws(string value)
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => Create(new Dictionary<string, string> { ["catName"] = value }));
        Assert.Equal(BusinessViolationCodes.InvalidNotificationRequest, ex.ErrorCode);
    }

    [Fact]
    public void RedactVariables_ReplacesAllValues_Idempotent()
    {
        var request = Create();
        var now = DateTimeOffset.UtcNow;

        request.RedactVariables(now);
        Assert.All(request.Variables.Values, v => Assert.Equal("[redacted]", v));
        Assert.Equal(now, request.VariablesRedactedOn);

        request.RedactVariables(now.AddMinutes(5));
        Assert.Equal(now, request.VariablesRedactedOn); // idempotent
    }

    [Fact]
    public void Suppress_SetsStatusAndReason()
    {
        var request = Create();
        request.Suppress(NotificationSuppressionReason.RecipientInactive, DateTimeOffset.UtcNow);

        Assert.Equal(NotificationRequestStatus.Suppressed, request.Status);
        Assert.Equal(NotificationSuppressionReason.RecipientInactive, request.SuppressionReason);
    }

    [Fact]
    public void MarkExpanded_SetsStatus()
    {
        var request = Create();
        var now = DateTimeOffset.UtcNow;
        request.MarkExpanded(now);

        Assert.Equal(NotificationRequestStatus.Expanded, request.Status);
        Assert.Equal(now, request.ProcessedOn);
    }

    /// <summary>
    /// SECURITY/CODE_REVIEW gate fix (2026-09-27, second pass, MEDIUM, "one bad row blocks the whole
    /// queue"). Below the terminal threshold: the request stays Pending, so
    /// <c>NotificationWorkQueue.GetPendingRequestIdsAsync</c> still retries it (just not first).
    /// </summary>
    [Fact]
    public void RecordFailedAttempt_BelowThreshold_StaysPending_TracksCountAndLastAttempt()
    {
        var request = Create();
        var first = DateTimeOffset.UtcNow;
        var second = first.AddMinutes(1);

        request.RecordFailedAttempt(first, maxAttempts: 5);
        Assert.Equal(NotificationRequestStatus.Pending, request.Status);
        Assert.Equal(1, request.AttemptCount);
        Assert.Equal(first, request.LastAttemptOn);

        request.RecordFailedAttempt(second, maxAttempts: 5);
        Assert.Equal(NotificationRequestStatus.Pending, request.Status);
        Assert.Equal(2, request.AttemptCount);
        Assert.Equal(second, request.LastAttemptOn);
    }

    [Fact]
    public void RecordFailedAttempt_ReachingMaxAttempts_BecomesTerminal_PersistenceFailed()
    {
        var request = Create();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 4; i++) request.RecordFailedAttempt(now.AddMinutes(i), maxAttempts: 5);
        Assert.Equal(NotificationRequestStatus.Pending, request.Status); // 4th attempt: still one short

        request.RecordFailedAttempt(now.AddMinutes(5), maxAttempts: 5);
        Assert.Equal(NotificationRequestStatus.Suppressed, request.Status);
        Assert.Equal(NotificationSuppressionReason.PersistenceFailed, request.SuppressionReason);
        Assert.Equal(5, request.AttemptCount);
    }

    /// <summary>Idempotent, the same way every other terminal-transition method on this aggregate
    /// is: a request that already reached a terminal status (here, expanded successfully in the
    /// meantime -- another worker pass resolved it before this stale attempt was recorded) must not
    /// be resurrected or overwritten.</summary>
    [Fact]
    public void RecordFailedAttempt_RequestAlreadyLeftPending_IsANoOp()
    {
        var request = Create();
        var now = DateTimeOffset.UtcNow;
        request.MarkExpanded(now);

        request.RecordFailedAttempt(now.AddMinutes(1), maxAttempts: 1);

        Assert.Equal(NotificationRequestStatus.Expanded, request.Status);
        Assert.Equal(0, request.AttemptCount);
        Assert.Null(request.LastAttemptOn);
    }
}
