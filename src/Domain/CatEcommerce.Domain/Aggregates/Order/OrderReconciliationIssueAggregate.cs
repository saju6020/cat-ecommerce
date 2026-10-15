using CatEcommerce.Domain.Events.Order;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Order;

/// <summary>
/// HO-ORD-001 §8.16 (ADR-205), US-ORD-08 AC2 -- a failure after a staff member entered payment details. The unit of work rolled back, so the details
/// would be lost; this record is written in a <b>fresh scope</b> and keeps what was entered so an accountant can finish the job by hand. It stores the
/// exception's <b>type</b> and a fixed message, never the exception's message (which may carry data). <c>Open</c> until an accountant resolves it with a
/// note.
/// </summary>
public sealed class OrderReconciliationIssueAggregate : AggregateRoot
{
    public Guid? OrderId { get; private set; }

    /// <summary>The branch the failure happened at (the order's, or the till's for a counter sale). Staff see and resolve only their active branch's issues; a row raised before this column existed is null and is not listed (SECURITY MEDIUM 2, 2026-10-09).</summary>
    public Guid? BranchId { get; private set; }
    public ReconciliationIssueKind Kind { get; private set; } = default!;
    public string PaymentMethod { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public string? PaymentReference { get; private set; }
    public DateTimeOffset? PaymentReceivedOn { get; private set; }
    public string ErrorSummary { get; private set; } = default!;
    public Guid CorrelationId { get; private set; }
    public Guid RaisedBy { get; private set; }
    public DateTimeOffset RaisedOn { get; private set; }
    public ReconciliationIssueStatus Status { get; private set; } = default!;
    public Guid? ResolvedBy { get; private set; }
    public DateTimeOffset? ResolvedOn { get; private set; }
    public string? ResolutionNote { get; private set; }

    private OrderReconciliationIssueAggregate() { }

    public OrderReconciliationIssueAggregate(Guid id) : base(id) { }

    public void Raise(ReconciliationIssueDto dto)
    {
        OrderId = dto.OrderId;
        BranchId = dto.BranchId;
        Kind = dto.Kind;
        PaymentMethod = Truncate(dto.Payment.Method, 30);
        Amount = dto.Payment.Amount;
        CurrencyCode = dto.CurrencyCode.ToUpperInvariant();
        // C-5: whatever the staff member typed, a card-like number is masked before it is kept (the table is never deleted from).
        PaymentReference = dto.Payment.Reference is null ? null : Truncate(CardNumberGuard.MaskCardNumbers(dto.Payment.Reference), OrderRules.MaxTransferReferenceLength);
        PaymentReceivedOn = dto.Payment.ReceivedOn;
        ErrorSummary = Truncate(dto.ErrorSummary, 200);
        CorrelationId = dto.CorrelationId;
        RaisedBy = dto.RaisedBy;
        RaisedOn = dto.RaisedOn;
        Status = ReconciliationIssueStatus.Open;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new ReconciliationIssueRaisedEvent { IssueId = Id, OrderId = OrderId, Kind = Kind.Value, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>An accountant closes the issue with a note saying what was done (<c>Open -> Resolved</c>). Resolving twice is refused.</summary>
    public void Resolve(Guid by, string note, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        if (Status != ReconciliationIssueStatus.Open)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReconciliationIssueAlreadyResolved, "This issue is already resolved.", nameof(Status));

        if (string.IsNullOrWhiteSpace(note) || note.Trim().Length > OrderRules.MaxReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReconciliationNoteRequired, $"Say what was done (up to {OrderRules.MaxReasonLength} characters).", nameof(note));

        Status = ReconciliationIssueStatus.Resolved;
        ResolvedBy = by;
        ResolvedOn = now;
        ResolutionNote = note.Trim();
        UpdateVersion();
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
