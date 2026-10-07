using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Account;

/// <summary>
/// HO-ACC-001 §8.4 / ADR-023's reasoning (`.ai/bounded-contexts.md` rule 3) -- sessions accumulate
/// without bound, so this is its own root, not an owned collection on the account. There is no
/// <c>Revive</c>/<c>Extend</c>/<c>Reactivate</c> -- an ended session is dead; signing in again
/// issues a new one.
/// </summary>
public sealed class CustomerSessionAggregate : AggregateRoot
{
    public Guid CustomerAccountId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public Guid IssuedForSecurityStamp { get; private set; }
    public DateTimeOffset IssuedOn { get; private set; }
    public DateTimeOffset ExpiresOn { get; private set; }
    public DateTimeOffset? EndedOn { get; private set; }
    public SessionEndReason? EndReason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    /// <summary>
    /// HO-ACC-001 §12.5 Amended (18) / ADR-342. The double-submit CSRF value's hash, generated the
    /// same way as the session token itself and stored on this durable row -- not derived from a
    /// process-lifetime key -- so it survives a restart exactly as the session it protects does.
    /// Only the hash lives here; the raw value goes to the client in the <c>pc_csrf</c> cookie and
    /// is never persisted a second time.
    /// </summary>
    public string CsrfTokenHash { get; private set; } = default!;

    private CustomerSessionAggregate() { }

    public CustomerSessionAggregate(Guid id) : base(id) { }

    public void Issue(SessionIssueDto dto)
    {
        CustomerAccountId = dto.SubjectAccountId;
        TokenHash = dto.TokenHash;
        IssuedForSecurityStamp = dto.IssuedForSecurityStamp;
        IssuedOn = dto.IssuedOn;
        ExpiresOn = dto.ExpiresOn;
        IpAddress = Truncate(dto.IpAddress, 45);
        UserAgent = Truncate(dto.UserAgent, 512);
        CsrfTokenHash = dto.CsrfTokenHash;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new CustomerSessionIssuedEvent
        {
            SessionId = Id,
            CustomerAccountId = CustomerAccountId,
            ExpiresOn = ExpiresOn,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void End(SessionEndDto dto)
    {
        if (EndedOn is not null) return; // idempotent re-call is a no-op

        EndedOn = dto.EndedOn;
        EndReason = dto.Reason;

        AddDomainEvent(new CustomerSessionEndedEvent
        {
            SessionId = Id,
            CustomerAccountId = CustomerAccountId,
            Reason = dto.Reason,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    /// <summary>Query, no mutation. Delegates to <see cref="SessionStateRules"/> so the aggregate
    /// and the read side cannot disagree.</summary>
    public SessionState StateAt(DateTimeOffset asOf, Guid currentStamp) =>
        SessionStateRules.Classify(EndedOn is not null, ExpiresOn, asOf, IssuedForSecurityStamp, currentStamp);

    /// <summary>
    /// HO-ACC-001 §12.5 Amended (18). Re-issued by a safe (GET) authenticated request when the
    /// client's <c>pc_csrf</c> cookie is missing or does not match this session's stored hash --
    /// this is what lets a legitimate browser recover on its own instead of being locked out for
    /// the life of the session. Not an audited change -- like <c>RecordActivity</c>, this is
    /// bookkeeping, not a business event, so no domain event is raised and <c>UpdateVersion()</c>
    /// is not called.
    ///
    /// ARCHITECTURE gate fifth pass, LOW (2026-09-25): guarded against an ended session, the same
    /// shape <see cref="StaffSessionAggregate.RecordActivity"/> already uses -- harmless today (the
    /// middleware only ever refreshes a session it has just resolved as active, and a refreshed
    /// hash on an ended session is never read), but a check the aggregate can make from its own
    /// state should not depend on every caller getting that right.</summary>
    public void RefreshCsrfToken(string newCsrfTokenHash)
    {
        if (EndedOn is not null)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.SessionInvalid, "This session has ended.", nameof(EndedOn));

        CsrfTokenHash = newCsrfTokenHash;
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null ? null : value.Length <= maxLength ? value : value[..maxLength];
}
