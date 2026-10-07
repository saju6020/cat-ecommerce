using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Account;

/// <summary>HO-ACC-001 §8.4. Same mechanism as <see cref="CustomerSessionAggregate"/>, different
/// subject and idle-expiry policy (US-ACC-08).</summary>
public sealed class StaffSessionAggregate : AggregateRoot
{
    public Guid StaffAccountId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public Guid IssuedForSecurityStamp { get; private set; }
    public DateTimeOffset IssuedOn { get; private set; }
    public DateTimeOffset ExpiresOn { get; private set; }
    public DateTimeOffset LastActivityOn { get; private set; }
    public DateTimeOffset? EndedOn { get; private set; }
    public SessionEndReason? EndReason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    /// <summary>HO-ACC-001 §12.5 Amended (18) / ADR-342. See
    /// <see cref="CustomerSessionAggregate.CsrfTokenHash"/> -- same mechanism, same reasoning.</summary>
    public string CsrfTokenHash { get; private set; } = default!;

    private static readonly TimeSpan ActivityThrottle = TimeSpan.FromSeconds(60);

    private StaffSessionAggregate() { }

    public StaffSessionAggregate(Guid id) : base(id) { }

    public void Issue(SessionIssueDto dto)
    {
        StaffAccountId = dto.SubjectAccountId;
        TokenHash = dto.TokenHash;
        IssuedForSecurityStamp = dto.IssuedForSecurityStamp;
        IssuedOn = dto.IssuedOn;
        ExpiresOn = dto.ExpiresOn;
        LastActivityOn = dto.IssuedOn;
        IpAddress = Truncate(dto.IpAddress, 45);
        UserAgent = Truncate(dto.UserAgent, 512);
        CsrfTokenHash = dto.CsrfTokenHash;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new StaffSessionIssuedEvent
        {
            SessionId = Id,
            StaffAccountId = StaffAccountId,
            ExpiresOn = ExpiresOn,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void End(SessionEndDto dto)
    {
        if (EndedOn is not null) return;

        EndedOn = dto.EndedOn;
        EndReason = dto.Reason;

        AddDomainEvent(new StaffSessionEndedEvent
        {
            SessionId = Id,
            StaffAccountId = StaffAccountId,
            Reason = dto.Reason,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public SessionState StateAt(DateTimeOffset asOf, Guid currentStamp, TimeSpan idleTimeout) =>
        SessionStateRules.Classify(EndedOn is not null, ExpiresOn, asOf, IssuedForSecurityStamp, currentStamp, LastActivityOn, idleTimeout);

    /// <summary>HO-ACC-001 §12.5 Amended (18). See
    /// <see cref="CustomerSessionAggregate.RefreshCsrfToken"/> -- same reasoning; not an audited
    /// change, no domain event, no version bump. ARCHITECTURE gate fifth pass, LOW (2026-09-25):
    /// guarded against an ended session, the same shape <see cref="RecordActivity"/> already
    /// uses.</summary>
    public void RefreshCsrfToken(string newCsrfTokenHash)
    {
        if (EndedOn is not null)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.SessionInvalid, "This session has ended.", nameof(EndedOn));

        CsrfTokenHash = newCsrfTokenHash;
    }

    /// <summary>Advances <see cref="LastActivityOn"/> only if at least 60s have passed since the
    /// stored value -- a write-amplification bound. Idle expiry is never late, at most 60s early
    /// (ADR-037). No event -- a heartbeat is not an audited change.</summary>
    public void RecordActivity(StaffSessionActivityDto dto)
    {
        // The caller (handler) is expected to have already classified the session as Active;
        // this guard exists so a programming error cannot silently advance a dead session.
        if (EndedOn is not null)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.SessionInvalid, "This session has ended.", nameof(EndedOn));

        if (dto.AsOf - LastActivityOn >= ActivityThrottle)
            LastActivityOn = dto.AsOf;
    }

    /// <summary>Terminal -- there is no <c>Revive</c>/<c>Extend</c>/<c>Reactivate</c>.</summary>
    public void EndForIdleTimeout(StaffSessionIdleTimeoutDto dto)
    {
        if (EndedOn is not null)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.SessionInvalid, "This session has already ended.", nameof(EndedOn));

        // Only the idle condition is re-checked here (not expiry/stamp) -- the handler has already
        // classified the session as IdleExpired via StateAt before calling this method.
        if (dto.AsOf - LastActivityOn < dto.IdleTimeout)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.SessionInvalid, "This session is not idle-expired.", nameof(LastActivityOn));

        var expiredOn = LastActivityOn + dto.IdleTimeout;
        EndedOn = expiredOn;
        EndReason = SessionEndReason.IdleTimeout;

        AddDomainEvent(new StaffSessionIdleExpiredEvent
        {
            SessionId = Id,
            StaffAccountId = StaffAccountId,
            LastActivityOn = LastActivityOn,
            ExpiredOn = expiredOn,
            DetectedOn = dto.AsOf,
            IdleTimeoutMinutes = (int)dto.IdleTimeout.TotalMinutes,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null ? null : value.Length <= maxLength ? value : value[..maxLength];
}
