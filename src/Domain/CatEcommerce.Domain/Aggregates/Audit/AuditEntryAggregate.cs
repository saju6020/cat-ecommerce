using System.Net;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Audit;

/// <summary>
/// Append-only leaf. Raises no domain event -- an event would be an audit of an audit
/// (SRS D-5). Written by domain event handlers, never by a command handler directly --
/// HO-CAT-001 section 8.4. Extended by HO-ACC-001 section 8.11 (US-ACC-12): IP, category and
/// correlation on every entry; <see cref="Record"/> becomes one-shot -- a wrong entry is answered
/// by another entry (D-5), never edited. No other public mutator may exist -- no Correct, Amend,
/// Redact or Void (U-A7).
/// </summary>
public sealed class AuditEntryAggregate : AggregateRoot
{
    private const int MaxEntityTypeOrActionLength = 100;
    private const int MaxReasonLength = 1000;

    public string EntityType { get; private set; } = default!;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = default!;
    public string Reason { get; private set; } = default!;
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset OccurredOn { get; private set; }
    public string? BeforeValue { get; private set; }
    public string? AfterValue { get; private set; }
    public IPAddress? IpAddress { get; private set; }
    public AuditCategory Category { get; private set; } = default!;
    public Guid? CorrelationId { get; private set; }
    public bool IsRecorded { get; private set; }

    private AuditEntryAggregate() { }

    public AuditEntryAggregate(Guid id) : base(id) { }

    public void Record(AuditEntryDto dto)
    {
        if (IsRecorded)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.AuditEntryAlreadyRecorded,
                "This audit entry has already been recorded.",
                nameof(IsRecorded));

        if (string.IsNullOrWhiteSpace(dto.EntityType) || dto.EntityType.Length > MaxEntityTypeOrActionLength)
            throw InvalidEntry(nameof(dto.EntityType));

        if (string.IsNullOrWhiteSpace(dto.Action) || dto.Action.Length > MaxEntityTypeOrActionLength)
            throw InvalidEntry(nameof(dto.Action));

        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > MaxReasonLength)
            throw InvalidEntry(nameof(dto.Reason));

        EntityType = dto.EntityType;
        EntityId = dto.EntityId;
        Action = dto.Action;
        Reason = dto.Reason;
        ActorUserId = dto.ActorUserId;
        OccurredOn = dto.OccurredOn;
        BeforeValue = dto.BeforeValue;
        AfterValue = dto.AfterValue;
        IpAddress = dto.IpAddress;
        Category = dto.Category;
        CorrelationId = dto.CorrelationId;
        IsRecorded = true;

        SetDefaultValue(dto.UserContext);

        UpdateVersion();
    }

    private static DomainRuleViolationException InvalidEntry(string propertyName) => new(
        BusinessViolationCodes.InvalidAuditEntry,
        "The audit entry is invalid.",
        propertyName);
}
