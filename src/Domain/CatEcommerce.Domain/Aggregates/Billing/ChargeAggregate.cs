using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 §8.3 (ADR-178) -- one billable thing, the only input to invoices. Recorded once (the unique
/// source key <c>(SourceKind, SourceId, SourceLineRef)</c> is the idempotency key) and immutable except
/// <see cref="Void"/>. <b>Whether it is billed is derived</b> (a live invoice line references it) and made
/// at-most-once by a filtered unique index on <c>invoice_line.charge_id</c> -- this aggregate never
/// records it. Raises <b>no domain event</b> (F31(c): a charge added by the push port never has its events
/// dispatched); a void is audited by the change-capture interceptor and a BusinessEvent row (§11.6).
/// </summary>
public sealed class ChargeAggregate : AggregateRoot
{
    public Guid CustomerId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid? CatId { get; private set; }
    public BusinessLine BusinessLine { get; private set; } = default!;
    public ItemKind ItemKind { get; private set; } = default!;
    public ChargeSourceKind SourceKind { get; private set; } = default!;
    public Guid SourceId { get; private set; }
    public string SourceLineRef { get; private set; } = default!;
    public BillingGroupKind? BillingGroupKind { get; private set; }
    public Guid? BillingGroupId { get; private set; }
    public string Description { get; private set; } = default!;
    public decimal Quantity { get; private set; }
    public decimal UnitPriceAmount { get; private set; }
    public decimal LineDiscountAmount { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public Guid TaxClassId { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public string? ReportingTag { get; private set; }
    public ChargeStatus Status { get; private set; } = default!;
    public DateTimeOffset RecordedOn { get; private set; }
    public Guid RecordedBy { get; private set; }
    public DateTimeOffset? VoidedOn { get; private set; }
    public Guid? VoidedBy { get; private set; }
    public string? VoidReason { get; private set; }

    public Money UnitPrice => new(UnitPriceAmount, CurrencyCode);
    public Money LineDiscount => new(LineDiscountAmount, CurrencyCode);

    private ChargeAggregate() { }

    public ChargeAggregate(Guid id) : base(id) { }

    public void Record(ChargeRequestDto request, ChargeRecordContext context)
    {
        if (request.UnitPrice.CurrencyCode != context.CurrencyCode || request.LineDiscount.CurrencyCode != context.CurrencyCode)
            throw new DomainRuleViolationException(BusinessViolationCodes.CurrencyMismatch, $"A charge must be in {context.CurrencyCode}.", nameof(request.UnitPrice));

        if (request.Quantity <= 0m || decimal.Round(request.Quantity, ChargeRules.MaxQuantityDecimals) != request.Quantity)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidQuantity, "A quantity must be greater than zero with at most three decimals.", nameof(request.Quantity));

        if (request.UnitPrice.Amount < 0m || request.LineDiscount.Amount < 0m || request.UnitPrice.Amount > BillingRules.MaxAmount)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "A price or discount cannot be negative.", nameof(request.UnitPrice));

        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > BillingRules.MaxDescriptionLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.DescriptionRequired, $"A description is required and cannot exceed {BillingRules.MaxDescriptionLength} characters.", nameof(request.Description));
        BillingFreeText.RefuseCardNumber(request.Description, nameof(request.Description));

        var lineAmount = Domain.DomainServices.InvoiceCalculator.Round(request.Quantity * request.UnitPrice.Amount);
        if (request.LineDiscount.Amount > lineAmount)
            throw new DomainRuleViolationException(BusinessViolationCodes.DiscountExceedsAmount, "A discount cannot exceed the line amount.", nameof(request.LineDiscount));

        var lineRef = string.IsNullOrWhiteSpace(request.SourceLineRef) ? ChargeRules.DefaultSourceLineRef : request.SourceLineRef.Trim();
        if (lineRef.Length > ChargeRules.MaxSourceLineRefLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidEnumValue, "The source line reference is too long.", nameof(request.SourceLineRef));

        if (request.ReportingTag is { Length: > ChargeRules.MaxReportingTagLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidTag, "The reporting tag is too long.", nameof(request.ReportingTag));
        BillingFreeText.RefuseCardNumber(request.ReportingTag, nameof(request.ReportingTag));

        if ((request.BillingGroupKind is null) != (request.BillingGroupId is null))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidEnumValue, "A billing group needs both a kind and an id.", nameof(request.BillingGroupId));

        if (!ChargeRules.MayOverrideBusinessLine(request.SourceKind) && request.BusinessLine != ChargeRules.DefaultBusinessLine(request.SourceKind))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidEnumValue, "The business line does not match the charge's source.", nameof(request.BusinessLine));

        CustomerId = request.CustomerId;
        BranchId = request.BranchId;
        CatId = request.CatId;
        BusinessLine = request.BusinessLine;
        ItemKind = request.ItemKind;
        SourceKind = request.SourceKind;
        SourceId = request.SourceId;
        SourceLineRef = lineRef;
        BillingGroupKind = request.BillingGroupKind;
        BillingGroupId = request.BillingGroupId;
        Description = request.Description.Trim();
        Quantity = request.Quantity;
        UnitPriceAmount = request.UnitPrice.Amount;
        LineDiscountAmount = request.LineDiscount.Amount;
        CurrencyCode = context.CurrencyCode;
        TaxClassId = request.TaxClassId;
        ServiceDate = request.ServiceDate;
        ReportingTag = request.ReportingTag;
        Status = ChargeStatus.Active;
        RecordedOn = context.Now;
        RecordedBy = context.Actor.UserId;

        SetDefaultValue(context.Actor.UserContext);
        UpdateVersion();
    }

    public void Void(string reason, DateTimeOffset now, BillingActor actor)
    {
        if (Status != ChargeStatus.Active)
            throw new DomainRuleViolationException(BusinessViolationCodes.ChargeNotActive, "This charge has already been voided.", nameof(Status));

        if (string.IsNullOrWhiteSpace(reason) || reason.Length > ChargeRules.MaxVoidReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReasonRequired, "A reason is required.", nameof(reason));
        BillingFreeText.RefuseCardNumber(reason, nameof(reason));

        Status = ChargeStatus.Voided;
        VoidedOn = now;
        VoidedBy = actor.UserId;
        VoidReason = reason.Trim();
        UpdateVersion();
    }

    /// <summary>HO-ACC-001 <c>CustomerMergedEvent</c> (ADR-192): an unbilled, active charge moves to the survivor.</summary>
    public void ReassignOwner(Guid newCustomerId)
    {
        if (CustomerId == newCustomerId || Status != ChargeStatus.Active) return;

        CustomerId = newCustomerId;
        UpdateVersion();
    }
}
