using CatEcommerce.Domain.Events.Clinic;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Clinic;

/// <summary>HO-APT-001 §8.11 (ADR-075) -- one entry per cat/service/date, oldest first
/// (<see cref="CreatedOn"/> is the queue order). <see cref="WaitlistStatus.Lapsed"/> is terminal
/// (OQ-8).</summary>
public sealed class WaitlistEntryAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid CatId { get; private set; }
    // Named ClinicServiceId, not ServiceId -- the platform's own Entity base class already
    // declares a ServiceId property (CS0108 hide warning otherwise; matches ServicePriceAggregate).
    public Guid ClinicServiceId { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public Guid? PreferredPerformerId { get; private set; }
    public WaitlistStatus Status { get; private set; } = default!;
    public Guid? OfferedAppointmentId { get; private set; }
    public DateTimeOffset? OfferExpiresOn { get; private set; }
    public DateTimeOffset CreatedOn { get; private set; }

    private WaitlistEntryAggregate() { }

    public WaitlistEntryAggregate(Guid id) : base(id) { }

    public void Join(WaitlistJoinDto dto)
    {
        BranchId = dto.BranchId;
        CustomerId = dto.CustomerId;
        CatId = dto.CatId;
        ClinicServiceId = dto.ServiceId;
        LocalDate = dto.LocalDate;
        PreferredPerformerId = dto.PreferredPerformerId;
        Status = WaitlistStatus.Waiting;
        CreatedOn = dto.Now;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new WaitlistJoinedEvent { WaitlistEntryId = Id, BranchId = BranchId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>Second root in the offer save, alongside the held appointment (ADR-075's exception).</summary>
    public void Offer(WaitlistOfferDto dto)
    {
        if (Status != WaitlistStatus.Waiting)
            throw new DomainRuleViolationException(BusinessViolationCodes.WaitlistEntryNotFound, "This waitlist entry is no longer waiting.", nameof(Status));

        Status = WaitlistStatus.Offered;
        OfferedAppointmentId = dto.AppointmentId;
        OfferExpiresOn = dto.OfferExpiresOn;

        AddDomainEvent(new WaitlistSlotOfferedEvent { WaitlistEntryId = Id, AppointmentId = dto.AppointmentId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>Read by id in the confirm handler, second root in the same save (ADR-075's exception).</summary>
    public void Claim(WaitlistClaimDto dto)
    {
        if (Status != WaitlistStatus.Offered) return;

        Status = WaitlistStatus.Claimed;
        UpdateVersion();
    }

    public void Lapse(WaitlistLapseDto dto)
    {
        if (Status != WaitlistStatus.Offered) return;

        Status = WaitlistStatus.Lapsed;
        OfferedAppointmentId = null;
        OfferExpiresOn = null;

        AddDomainEvent(new WaitlistOfferLapsedEvent { WaitlistEntryId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Withdraw(WaitlistWithdrawDto dto)
    {
        if (Status != WaitlistStatus.Waiting && Status != WaitlistStatus.Offered) return;

        Status = WaitlistStatus.Withdrawn;
        UpdateVersion();
    }

    /// <summary>The day passed with no offer made.</summary>
    public void Close(WaitlistCloseDto dto)
    {
        if (Status != WaitlistStatus.Waiting) return;

        Status = WaitlistStatus.Closed;
        UpdateVersion();
    }

    /// <summary>HO-ACC-001 <c>CustomerMergedEvent</c> contract (§11.6).</summary>
    public void ReassignOwner(Guid newCustomerId)
    {
        if (CustomerId == newCustomerId) return;

        CustomerId = newCustomerId;
        UpdateVersion();
    }
}
