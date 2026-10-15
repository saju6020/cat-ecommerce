using CatEcommerce.Domain.Events.Prescription;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Prescription;

/// <summary>
/// HO-PHM-001 §8.10 / US-PHM-08 (ADR-149) -- a vet's authorisation of a named therapeutic food for ONE cat, for a period of branch-local dates
/// (inclusive both ends). The authorisation names the PRODUCT ("Renal diet X"), not the 2 kg bag; the gate takes a SKU and resolves its product.
/// "Active" here is the stored state; whether the food is purchasable is decided by the gate on every call from the dates, so the product locks again
/// at midnight whether or not the expiry sweep has run -- the sweep only records the expiry, raises the event and tells the owner.
/// At most one <c>Active</c> authorisation per cat and product (filtered unique index as the race guard).
/// </summary>
public sealed class DietAuthorisationAggregate : AggregateRoot
{
    public Guid CatId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductNameSnapshot { get; private set; } = default!;
    public Guid VisitId { get; private set; }
    public Guid AuthorisedBy { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly ValidTo { get; private set; }
    public string? Notes { get; private set; }
    public DietAuthorisationStatus Status { get; private set; } = DietAuthorisationStatus.Active;
    public DateOnly? ExpiredOn { get; private set; }
    public DateTimeOffset? RevokedOn { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public string? RevocationReason { get; private set; }
    public int ExtensionCount { get; private set; }

    private DietAuthorisationAggregate() { }

    public DietAuthorisationAggregate(Guid id) : base(id) { }

    public void Authorise(DietAuthorisationDto dto)
    {
        if (dto.ValidFrom < dto.Today.AddDays(-1) || dto.ValidTo < dto.ValidFrom || dto.ValidTo > dto.ValidFrom.AddDays(dto.MaxDays))
            throw Violation(BusinessViolationCodes.InvalidDietValidity,
                $"The authorisation starts no earlier than yesterday and runs for at most {dto.MaxDays} days.", nameof(dto.ValidTo));

        if ((dto.Notes?.Length ?? 0) > DietRules.MaxNotesLength)
            throw Violation(BusinessViolationCodes.InvalidDietValidity, "Notes are at most 500 characters.", nameof(dto.Notes));

        CatId = dto.CatId;
        CustomerId = dto.CustomerId;
        ProductId = dto.ProductId;
        ProductNameSnapshot = dto.ProductNameSnapshot;
        VisitId = dto.VisitId;
        AuthorisedBy = dto.AuthorisedBy;
        ValidFrom = dto.ValidFrom;
        ValidTo = dto.ValidTo;
        Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        Status = DietAuthorisationStatus.Active;
        ExtensionCount = 0;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new DietAuthorisationIssuedEvent
        {
            AuthorisationId = Id, CatId = CatId, CustomerId = CustomerId, ProductId = ProductId, ValidFrom = ValidFrom, ValidTo = ValidTo,
            CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext
        });
        UpdateVersion();
    }

    /// <summary>Whether the sweep should record the expiry: stored as active and the last day has passed.</summary>
    public bool CanExpire(DateOnly today) => Status == DietAuthorisationStatus.Active && today > ValidTo;

    public void Expire(DateOnly today, PharmacyActor actor)
    {
        if (!CanExpire(today))
            throw Violation(BusinessViolationCodes.DietAuthorisationNotExtendable, "This authorisation has not ended.", nameof(Status));

        Status = DietAuthorisationStatus.Expired;
        ExpiredOn = today;

        AddDomainEvent(new DietAuthorisationExpiredEvent
        {
            AuthorisationId = Id, CatId = CatId, CustomerId = CustomerId, ProductId = ProductId, ValidTo = ValidTo,
            CorrelationId = actor.CorrelationId, UserContext = actor.UserContext
        });
        UpdateVersion();
    }

    /// <summary>Extends an active authorisation, or re-activates one that lapsed within the last 30 days (a renewal after a short gap).</summary>
    public void Extend(DateOnly newValidTo, DateOnly today, int maxDays, PharmacyActor actor)
    {
        var lapsedRecently = Status == DietAuthorisationStatus.Expired && today <= ValidTo.AddDays(DietRules.ReactivationWindowDays);
        if (Status != DietAuthorisationStatus.Active && !lapsedRecently)
            throw Violation(BusinessViolationCodes.DietAuthorisationNotExtendable, "Only an active authorisation, or one that ended in the last 30 days, can be extended.", nameof(Status));

        if (newValidTo <= ValidTo || newValidTo > today.AddDays(maxDays))
            throw Violation(BusinessViolationCodes.InvalidDietValidity, $"The new end date is after the current one and within {maxDays} days of today.", nameof(newValidTo));

        ValidTo = newValidTo;
        Status = DietAuthorisationStatus.Active;
        ExpiredOn = null;
        ExtensionCount++;

        AddDomainEvent(new DietAuthorisationExtendedEvent
        {
            AuthorisationId = Id, CatId = CatId, CustomerId = CustomerId, ProductId = ProductId, ValidTo = ValidTo,
            CorrelationId = actor.CorrelationId, UserContext = actor.UserContext
        });
        UpdateVersion();
    }

    public void Revoke(Guid by, string reason, DateTimeOffset now, PharmacyActor actor)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > DietRules.MaxReasonLength)
            throw Violation(BusinessViolationCodes.RevocationReasonRequired, "Give a reason of at most 500 characters.", nameof(reason));

        if (Status != DietAuthorisationStatus.Active)
            throw Violation(BusinessViolationCodes.DietAuthorisationNotExtendable, "Only an active authorisation can be revoked.", nameof(Status));

        Status = DietAuthorisationStatus.Revoked;
        RevokedOn = now;
        RevokedBy = by;
        RevocationReason = reason.Trim();

        AddDomainEvent(new DietAuthorisationRevokedEvent
        {
            AuthorisationId = Id, CatId = CatId, CustomerId = CustomerId, ProductId = ProductId, ValidTo = ValidTo,
            CorrelationId = actor.CorrelationId, UserContext = actor.UserContext
        });
        UpdateVersion();
    }

    /// <summary>A customer merge moves the authorisation to the survivor (ADR-152).</summary>
    public void ReassignCustomer(Guid survivorId)
    {
        if (CustomerId == survivorId) return;
        CustomerId = survivorId;
        UpdateVersion();
    }

    private static DomainRuleViolationException Violation(string code, string message, string property) => new(code, message, property);
}
