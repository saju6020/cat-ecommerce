using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Notification;

/// <summary>
/// HO-NTF-001 §8.6 / ADR-060 -- one message on one channel: at once the outbox item, the delivery
/// log and (for <see cref="NotificationChannel.InApp"/>) the inbox item. No domain events --
/// nothing subscribes; the worker reads state, not events.
/// </summary>
public sealed class NotificationAggregate : AggregateRoot
{
    private static readonly HashSet<NotificationStatus> TerminalStatuses =
    [
        NotificationStatus.Sent, NotificationStatus.Undeliverable, NotificationStatus.Expired,
        NotificationStatus.Cancelled, NotificationStatus.InboxDelivered
    ];

    private readonly List<DeliveryAttempt> _attempts = [];

    public Guid RequestId { get; private set; }
    public string TemplateKey { get; private set; } = default!;
    public int TemplateVersion { get; private set; }
    public string Locale { get; private set; } = default!;
    public NotificationCategory Category { get; private set; } = default!;
    public NotificationUrgency Urgency { get; private set; } = default!;
    public bool ContainsSecret { get; private set; }
    public NotificationRecipientKind RecipientKind { get; private set; } = default!;
    public Guid RecipientId { get; private set; }
    public NotificationSubjectKind? SubjectKind { get; private set; }
    public Guid? SubjectId { get; private set; }
    public Guid? SubjectRef { get; private set; }
    public DateOnly? SubjectDueOn { get; private set; }

    /// <summary>HO-APT-001 §8.9 amendment -- carried from the request so the send-time guard can
    /// be re-checked immediately before each delivery attempt, not only at expansion (§8.7).</summary>
    public string? SubjectStamp { get; private set; }

    /// <summary>HO-RPT-001 section 11.7 (HO-NTF-001 amendment): a file the message carries, by reference, loaded immediately before sending (Email only). No attachment bytes ever enter notify.*.</summary>
    public NotificationAttachmentKind? AttachmentKind { get; private set; }

    public Guid? AttachmentId { get; private set; }
    public Guid? BranchId { get; private set; }
    public NotificationChannel Channel { get; private set; } = default!;
    public string? Destination { get; private set; }
    public string? Subject { get; private set; }
    public string? Title { get; private set; }
    public string Body { get; private set; } = default!;
    public string? HtmlBody { get; private set; }
    public string? LinkPath { get; private set; }
    public NotificationStatus Status { get; private set; } = default!;
    public DateTimeOffset? NotBefore { get; private set; }
    public bool DeferredForQuietHours { get; private set; }
    public DateTimeOffset? NotAfter { get; private set; }
    public DateTimeOffset? NextAttemptOn { get; private set; }
    public DateTimeOffset? LeaseExpiresOn { get; private set; }
    public int AttemptCount { get; private set; }

    /// <summary>How many times <see cref="Claim"/> has run. Unlike <see cref="AttemptCount"/> it also
    /// counts a claim whose attempt was never recorded (save failure after the claim), so a row
    /// that keeps crashing mid-send reaches <see cref="NotificationRetryRules.MaxAttempts"/> and is
    /// parked instead of being re-sent every lease forever (NTF-001 backlog row 5).</summary>
    public int ClaimCount { get; private set; }
    public IReadOnlyList<DeliveryAttempt> Attempts => _attempts;
    public Guid? FallbackFromId { get; private set; }
    public Guid? ResendOfId { get; private set; }
    public Guid? ResentBy { get; private set; }

    /// <summary>Gate fix 2026-09-27 (SECURITY LOW). This resend's 1-based position in its chain;
    /// <c>null</c> for a non-resend. See <see cref="Models.NotificationCreateDto"/>.</summary>
    public int? ResendOrdinal { get; private set; }
    public string? UnsubscribeTokenHash { get; private set; }

    /// <summary>Deviation beyond §8.6's own field list (recorded in the implementation report):
    /// the rendered unsubscribe URL, kept alongside the hash so C5 can build the email
    /// <c>List-Unsubscribe</c>/<c>List-Unsubscribe-Post</c> headers on every delivery attempt
    /// (including retries) without re-parsing the rendered body. Not a secret -- the same URL is
    /// already plainly visible in the message body itself.</summary>
    public string? UnsubscribeLink { get; private set; }
    public DateTimeOffset? ReadOn { get; private set; }
    public DateTimeOffset CreatedOn { get; private set; }
    public DateTimeOffset? SentOn { get; private set; }
    public DateTimeOffset? ClosedOn { get; private set; }
    public NotificationCloseReason? CloseReason { get; private set; }
    public DateTimeOffset? RedactedOn { get; private set; }

    /// <summary>Gate fix 2026-09-27 (SECURITY/ARCHITECTURE/CODE_REVIEW HIGH). Which of §8.6's
    /// reasons produced <see cref="RedactedOn"/> -- <see cref="Anonymisation"/> takes precedence
    /// over <see cref="Secret"/> once both have applied, so the read side's <c>redactedReason</c>
    /// always reflects the strongest reason a row has ever been redacted for.</summary>
    public NotificationRedactionReason? RedactedReason { get; private set; }

    /// <summary>Gate fix 2026-09-27. Separate from <see cref="RedactedOn"/> so an anonymisation
    /// redaction is independently idempotent: a row already <see cref="Secret"/>-redacted (the
    /// normal case for a delivered password reset, within minutes of being sent) must still have
    /// its destination nulled the first time <see cref="Redact"/> is called with
    /// <see cref="NotificationRedactionReason.Anonymisation"/> -- checking <see cref="RedactedOn"/>
    /// alone would have made that call a no-op forever.</summary>
    public DateTimeOffset? AnonymisedOn { get; private set; }

    private NotificationAggregate() { }

    public NotificationAggregate(Guid id) : base(id) { }

    public void CreateExternal(NotificationCreateDto dto)
    {
        ApplyCommon(dto);
        Channel = dto.Channel;
        Destination = dto.Destination;
        NotBefore = dto.NotBefore;
        DeferredForQuietHours = dto.DeferredForQuietHours;
        Status = NotificationStatus.Queued;
        UnsubscribeTokenHash = dto.UnsubscribeTokenHash;
        UnsubscribeLink = dto.UnsubscribeLink;

        UpdateVersion();
    }

    public void CreateInbox(NotificationCreateDto dto)
    {
        ApplyCommon(dto);
        Channel = NotificationChannel.InApp;
        Destination = null;
        Status = NotificationStatus.InboxDelivered;
        SentOn = dto.CreatedOn;

        UpdateVersion();
    }

    private void ApplyCommon(NotificationCreateDto dto)
    {
        RequestId = dto.RequestId;
        TemplateKey = dto.TemplateKey;
        TemplateVersion = dto.TemplateVersion;
        Locale = dto.Locale;
        Category = dto.Category;
        Urgency = dto.Urgency;
        ContainsSecret = dto.ContainsSecret;
        RecipientKind = dto.RecipientKind;
        RecipientId = dto.RecipientId;
        SubjectKind = dto.SubjectKind;
        SubjectId = dto.SubjectId;
        SubjectRef = dto.SubjectRef;
        SubjectDueOn = dto.SubjectDueOn;
        SubjectStamp = dto.SubjectStamp;
        AttachmentKind = dto.AttachmentKind;
        AttachmentId = dto.AttachmentId;
        BranchId = dto.BranchId;
        Subject = dto.Subject;
        Title = dto.Title;
        Body = dto.Body;
        HtmlBody = dto.HtmlBody;
        LinkPath = dto.LinkPath;
        NotAfter = dto.NotAfter;
        CreatedOn = dto.CreatedOn;
        FallbackFromId = dto.FallbackFromId;
        ResendOfId = dto.ResendOfId;
        ResentBy = dto.ResentBy;
        ResendOrdinal = dto.ResendOrdinal;
    }

    /// <summary>Whether <see cref="Claim"/> would succeed right now -- exposed so C5 can shortcut
    /// ("terminal or not due -> success", §11.4) without duplicating this predicate.</summary>
    public bool CanClaim(DateTimeOffset now)
    {
        var dueFromQueue = (Status == NotificationStatus.Queued || Status == NotificationStatus.RetryPending)
            && (NotBefore is null || NotBefore <= now)
            && (NextAttemptOn is null || NextAttemptOn <= now);

        var crashedMidSend = Status == NotificationStatus.Sending && LeaseExpiresOn is not null && LeaseExpiresOn < now;

        return dueFromQueue || crashedMidSend;
    }

    /// <summary>The claim itself is committed before the provider is ever called -- <c>xmin</c>
    /// stops two workers claiming the same row (ADR-060).</summary>
    public void Claim(DateTimeOffset now, TimeSpan lease)
    {
        if (!CanClaim(now))
            throw new DomainRuleViolationException(BusinessViolationCodes.NotificationNotDue, "This notification is not due for delivery.", nameof(Status));

        Status = NotificationStatus.Sending;
        LeaseExpiresOn = now + lease;
        ClaimCount++;
        UpdateVersion();
    }

    /// <summary>True for a row stuck <see cref="NotificationStatus.Sending"/> whose lease has
    /// expired and which has already been claimed the maximum number of times.</summary>
    public bool MustPark(DateTimeOffset now) =>
        Status == NotificationStatus.Sending && LeaseExpiresOn is not null && LeaseExpiresOn < now
        && ClaimCount >= NotificationRetryRules.MaxAttempts;

    /// <summary>Gives up on a row <see cref="MustPark"/> says is stuck: records a permanent-failure
    /// attempt (audit trail) and moves it to <see cref="NotificationStatus.Undeliverable"/>.</summary>
    public void Park(DateTimeOffset now)
    {
        if (!MustPark(now))
            throw new DomainRuleViolationException(BusinessViolationCodes.NotificationNotDue, "This notification is not stuck.", nameof(Status));

        RecordAttempt(SendOutcomeKind.PermanentFailure, now, "None", null, "ParkedAfterRepeatedClaims");
    }

    public void RecordAttempt(SendOutcomeKind outcome, DateTimeOffset now, string providerName, string? providerMessageId, string? providerResponse)
    {
        _attempts.Add(new DeliveryAttempt(Guid.NewGuid(), now, outcome, providerName, providerMessageId, providerResponse));
        AttemptCount++;

        if (outcome == SendOutcomeKind.Accepted)
        {
            Status = NotificationStatus.Sent;
            SentOn = now;
        }
        else if (outcome == SendOutcomeKind.TransientFailure && AttemptCount < NotificationRetryRules.MaxAttempts)
        {
            Status = NotificationStatus.RetryPending;
            NextAttemptOn = now + NotificationRetryRules.BackoffFor(AttemptCount);
        }
        else
        {
            Status = NotificationStatus.Undeliverable;
        }

        UpdateVersion();
    }

    /// <summary>Only from <see cref="NotificationStatus.Queued"/>/<see cref="NotificationStatus.RetryPending"/>.</summary>
    public void Defer(DateTimeOffset until)
    {
        if (Status != NotificationStatus.Queued && Status != NotificationStatus.RetryPending)
            throw new DomainRuleViolationException(BusinessViolationCodes.NotificationNotDue, "Only a queued or retry-pending notification can be deferred.", nameof(Status));

        NotBefore = until;
        DeferredForQuietHours = true;
        UpdateVersion();
    }

    public void Expire(DateTimeOffset now)
    {
        if (TerminalStatuses.Contains(Status)) return;
        Status = NotificationStatus.Expired;
        ClosedOn = now;
        CloseReason = NotificationCloseReason.NotAfterPassed;
        UpdateVersion();
    }

    public void Cancel(NotificationCloseReason reason, DateTimeOffset now)
    {
        if (TerminalStatuses.Contains(Status)) return;
        Status = NotificationStatus.Cancelled;
        ClosedOn = now;
        CloseReason = reason;
        UpdateVersion();
    }

    /// <summary>HO-ACC-001 ADR-039 -- moves an in-app inbox item to the merge survivor. Idempotent
    /// (a no-op if already the survivor, which the handler's loop termination already relies on).
    /// The external log keeps the original recipient -- it is history (§11.6).</summary>
    public void ReassignRecipientOnMerge(Guid survivingCustomerId)
    {
        if (RecipientId == survivingCustomerId) return;

        RecipientId = survivingCustomerId;
        UpdateVersion();
    }

    /// <summary><c>InApp</c> only; idempotent.</summary>
    public void MarkRead(DateTimeOffset now)
    {
        if (Channel != NotificationChannel.InApp)
            throw new DomainRuleViolationException(BusinessViolationCodes.NotificationNotFound, "Only an in-app notification can be marked read.", nameof(Channel));

        ReadOn ??= now;
        UpdateVersion();
    }

    /// <summary>
    /// §8.6/§8.10 rule 3. Body/subject/title/HTML/link redaction is idempotent on
    /// <see cref="RedactedOn"/>. <see cref="NotificationRedactionReason.Anonymisation"/> is
    /// idempotent on its OWN separate marker (<see cref="AnonymisedOn"/>) instead, and always nulls
    /// the destination and the unsubscribe link the first time it runs -- including on a row
    /// already <see cref="NotificationRedactionReason.Secret"/>-redacted, which is the normal case
    /// for a delivered password reset (it is redacted as soon as it reaches a terminal state,
    /// typically minutes after being sent, long before an anonymisation request could ever reach
    /// it). Without this split, anonymisation was a permanent no-op for every such row (gate fix
    /// 2026-09-27, SECURITY/ARCHITECTURE/CODE_REVIEW HIGH; A16/ADR-041).
    /// <see cref="RedactedReason"/> always reflects the strongest reason applied so far --
    /// <see cref="NotificationRedactionReason.Anonymisation"/> overrides a prior
    /// <see cref="NotificationRedactionReason.Secret"/>, never the other way round.
    /// </summary>
    public void Redact(NotificationRedactionReason reason, DateTimeOffset now)
    {
        if (reason == NotificationRedactionReason.Retention)
        {
            // HO-XCT-001 section 5.3 / ADR-305: content is redacted retention.notificationContentMonths after creation, like an anonymisation it also nulls
            // the destination and the unsubscribe link, but it is NOT an anonymisation (the recipient still exists) and never overrides one. Metadata (channel,
            // template, status, timestamps, recipient id) is kept. Idempotent.
            if (AnonymisedOn is not null || RedactedReason == NotificationRedactionReason.Retention) return;

            RedactBodyFieldsIfNotAlready();
            Destination = null;
            UnsubscribeLink = null;

            RedactedOn ??= now;
            RedactedReason = NotificationRedactionReason.Retention;
            UpdateVersion();
            return;
        }

        if (reason == NotificationRedactionReason.Anonymisation)
        {
            if (AnonymisedOn is not null) return;

            RedactBodyFieldsIfNotAlready();
            Destination = null;
            UnsubscribeLink = null;

            AnonymisedOn = now;
            RedactedOn ??= now;
            RedactedReason = NotificationRedactionReason.Anonymisation;
            UpdateVersion();
            return;
        }

        if (RedactedOn is not null) return;

        RedactBodyFieldsIfNotAlready();
        RedactedOn = now;
        RedactedReason = NotificationRedactionReason.Secret;
        UpdateVersion();
    }

    private void RedactBodyFieldsIfNotAlready()
    {
        Body = "[redacted]";
        if (Subject is not null) Subject = "[redacted]";
        if (Title is not null) Title = "[redacted]";
        if (HtmlBody is not null) HtmlBody = "[redacted]";
        if (LinkPath is not null) LinkPath = "[redacted]";
    }

    public bool IsTerminal => TerminalStatuses.Contains(Status);
}
