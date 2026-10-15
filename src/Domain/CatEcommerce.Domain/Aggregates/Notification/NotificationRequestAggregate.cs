using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Notification;

/// <summary>
/// HO-NTF-001 §8.4 / ADR-056/057 -- the outbox item. No domain events (nothing subscribes; the
/// worker reads state, not events).
/// </summary>
public sealed class NotificationRequestAggregate : AggregateRoot
{
    public string TemplateKey { get; private set; } = default!;
    public NotificationRecipientKind RecipientKind { get; private set; } = default!;
    public Guid RecipientId { get; private set; }
    public IReadOnlyDictionary<string, string> Variables { get; private set; } = new Dictionary<string, string>();
    public NotificationChannel? ChannelHint { get; private set; }
    public NotificationSubjectKind? SubjectKind { get; private set; }
    public Guid? SubjectId { get; private set; }
    public Guid? SubjectRef { get; private set; }
    public DateOnly? SubjectDueOn { get; private set; }
    public Guid? BranchId { get; private set; }
    public string? DeduplicationKey { get; private set; }
    public DateTimeOffset? NotAfter { get; private set; }

    /// <summary>HO-APT-001 §8.9 amendment (§1 Amended, HO-NTF-001). The request is not expanded
    /// before this instant -- honoured by <c>NotificationWorkQueue</c>, not this aggregate (which
    /// has no clock).</summary>
    public DateTimeOffset? SendAfter { get; private set; }

    /// <summary>HO-APT-001 §8.9 amendment. Compared by a subject guard (e.g.
    /// <c>AppointmentReminderGuard</c>) against the subject's own current stamp -- a producer
    /// invalidates a stale, already-enqueued request by enqueueing a fresh one with a new stamp;
    /// the old one then fails its guard at expansion (or delivery) time.</summary>
    public string? SubjectStamp { get; private set; }

    /// <summary>HO-RPT-001 section 11.7 (HO-NTF-001 amendment): a file the message carries, by reference. No attachment bytes ever enter notify.*.</summary>
    public NotificationAttachmentKind? AttachmentKind { get; private set; }

    public Guid? AttachmentId { get; private set; }

    public bool ContainsSecret { get; private set; }
    public NotificationRequestStatus Status { get; private set; } = default!;
    public NotificationSuppressionReason? SuppressionReason { get; private set; }
    public DateTimeOffset RequestedOn { get; private set; }
    public DateTimeOffset? ProcessedOn { get; private set; }
    public Guid RequestedBy { get; private set; }
    public Guid CorrelationId { get; private set; }
    public DateTimeOffset? VariablesRedactedOn { get; private set; }

    /// <summary>Gate fix 2026-09-27 (SECURITY/CODE_REVIEW second pass, MEDIUM). Counts failed
    /// EXPANSION attempts that got past rendering but failed to save (never render failures --
    /// those are suppressed immediately by <see cref="Suppress"/>). See
    /// <see cref="RecordFailedAttempt"/>.</summary>
    public int AttemptCount { get; private set; }

    /// <summary>Null until the first failed attempt. Used to order the pending queue so a
    /// repeatedly-failing row sorts behind every request that has never been attempted yet
    /// (<c>NotificationWorkQueue.GetPendingRequestIdsAsync</c>).</summary>
    public DateTimeOffset? LastAttemptOn { get; private set; }

    private NotificationRequestAggregate() { }

    public NotificationRequestAggregate(Guid id) : base(id) { }

    public void Create(NotificationRequestCreateDto dto)
    {
        if (dto.Variables.Count > NotificationRequestRules.MaxVariables)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidNotificationRequest,
                $"A request may carry at most {NotificationRequestRules.MaxVariables} variables.", nameof(dto.Variables));

        if (dto.Variables.Values.Any(v => v.Length > NotificationRequestRules.MaxVariableLength))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidNotificationRequest,
                $"A variable value cannot exceed {NotificationRequestRules.MaxVariableLength} characters.", nameof(dto.Variables));

        // Gate fix 2026-09-27 (SECURITY/CODE_REVIEW MEDIUM 7): defense in depth alongside
        // TemplateRules' own rejection of these characters in the template itself -- a variable
        // VALUE carrying the renderer's sentinel code points (or any other control/private-use/
        // format character) would otherwise reach NotificationRenderer.Substitute unescaped.
        if (dto.Variables.Values.Any(v => ControlCharacters.ContainsDisallowed(v) || ControlCharacters.ContainsPrivateUseOrFormat(v)))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidNotificationRequest,
                "A variable value contains a disallowed character.", nameof(dto.Variables));

        if (dto.DeduplicationKey?.Length > NotificationRequestRules.MaxDeduplicationKeyLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidNotificationRequest,
                $"DeduplicationKey cannot exceed {NotificationRequestRules.MaxDeduplicationKeyLength} characters.", nameof(dto.DeduplicationKey));

        TemplateKey = dto.TemplateKey;
        RecipientKind = dto.RecipientKind;
        RecipientId = dto.RecipientId;
        Variables = new Dictionary<string, string>(dto.Variables);
        ChannelHint = dto.ChannelHint;
        SubjectKind = dto.SubjectKind;
        SubjectId = dto.SubjectId;
        SubjectRef = dto.SubjectRef;
        SubjectDueOn = dto.SubjectDueOn;
        BranchId = dto.BranchId;
        DeduplicationKey = dto.DeduplicationKey;
        NotAfter = dto.NotAfter;
        SendAfter = dto.SendAfter;
        SubjectStamp = dto.SubjectStamp;
        AttachmentKind = dto.AttachmentKind;
        AttachmentId = dto.AttachmentId;
        ContainsSecret = dto.ContainsSecret;
        Status = NotificationRequestStatus.Pending;
        RequestedOn = dto.RequestedOn;
        RequestedBy = dto.RequestedBy;
        CorrelationId = dto.CorrelationId;

        UpdateVersion();
    }

    public void MarkExpanded(DateTimeOffset asOf)
    {
        Status = NotificationRequestStatus.Expanded;
        ProcessedOn = asOf;
        UpdateVersion();
    }

    public void Suppress(NotificationSuppressionReason reason, DateTimeOffset asOf)
    {
        Status = NotificationRequestStatus.Suppressed;
        SuppressionReason = reason;
        ProcessedOn = asOf;
        UpdateVersion();
    }

    public void Expire(DateTimeOffset asOf)
    {
        Status = NotificationRequestStatus.Expired;
        ProcessedOn = asOf;
        UpdateVersion();
    }

    /// <summary>Idempotent (§8.10 rule 3).</summary>
    public void RedactVariables(DateTimeOffset asOf)
    {
        if (VariablesRedactedOn is not null) return;

        Variables = Variables.Keys.ToDictionary(k => k, _ => "[redacted]");
        VariablesRedactedOn = asOf;
        UpdateVersion();
    }

    /// <summary>
    /// Gate fix 2026-09-27 (SECURITY/CODE_REVIEW second pass, MEDIUM, "one bad row blocks the whole
    /// queue"). Called when expansion got past rendering (a render failure suppresses immediately,
    /// on the FIRST attempt -- see <see cref="Suppress"/> with
    /// <c>NotificationSuppressionReason.RenderFailed</c>) but the save itself failed, for any
    /// reason. A no-op once the request has already left <see cref="NotificationRequestStatus.Pending"/>
    /// (another attempt, or another worker, already resolved it) -- idempotent the same way every
    /// other terminal-transition method on this aggregate is.
    /// </summary>
    public void RecordFailedAttempt(DateTimeOffset asOf, int maxAttempts)
    {
        if (Status != NotificationRequestStatus.Pending) return;

        AttemptCount++;
        LastAttemptOn = asOf;

        if (AttemptCount >= maxAttempts)
        {
            Status = NotificationRequestStatus.Suppressed;
            SuppressionReason = NotificationSuppressionReason.PersistenceFailed;
            ProcessedOn = asOf;
        }

        UpdateVersion();
    }
}
