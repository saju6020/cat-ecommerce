using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Surgery;

/// <summary>HO-SRG-001 §11.5. Ids, instants, enums and booleans ONLY -- no reasons, notes, instructions or names (U-X1). A handler that needs a reason loads the aggregate.</summary>
public sealed class SurgeryScheduledEvent : Event
{
    public required Guid SurgeryId { get; init; }
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid ServiceId { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public required SurgeryPricingBasis PricingBasis { get; init; }
}

public sealed class SurgeryRescheduledEvent : Event
{
    public required Guid SurgeryId { get; init; }
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CatId { get; init; }
    public required DateTimeOffset StartsOn { get; init; }
    public required DateTimeOffset PreviousStartsOn { get; init; }
}

public sealed class SurgeryCancelledEvent : Event
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CatId { get; init; }
}

/// <summary>HO-SRG-001 §8.13 E2 -- HO-EMR-001 may open the visit with <c>VisitId</c> from this (the contract EMR confirms).</summary>
public sealed class SurgeryAdmittedEvent : Event
{
    public required Guid SurgeryId { get; init; }
    public required Guid VisitId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid SurgeonId { get; init; }
    public required DateTimeOffset AdmittedOn { get; init; }
}

public sealed class SurgeryStartedEvent : Event
{
    public required Guid SurgeryId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
    public required bool ConsentWasOverridden { get; init; }
}

public sealed class SurgeryConsentOverriddenEvent : Event
{
    public required Guid SurgeryId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid OverriddenBy { get; init; }
}

public sealed class SurgeryProcedureEndedEvent : Event
{
    public required Guid SurgeryId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
    public required ProcedureOutcome Outcome { get; init; }
}

/// <summary>HO-SRG-001 §8.14 (ADR-166) -- raised only for a Completed spay or neuter. The Cat context sets the neuter status from it.</summary>
public sealed class SterilisationCompletedEvent : Event
{
    public required Guid SurgeryId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
    public required DateOnly CompletedOnLocalDate { get; init; }
}

public sealed class RecoveryStatusChangedEvent : Event
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required RecoveryStatus Status { get; init; }
}

/// <summary>The HO-BIL-001 and HO-BRD-001 contract: carries <c>SurgeryId</c>, <c>CatId</c> and <c>BranchId</c> so a boarding stay can be linked on its side.</summary>
public sealed class SurgeryDischargedEvent : Event
{
    public required Guid SurgeryId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid ServiceId { get; init; }
    public required DateTimeOffset DischargedOn { get; init; }
    public required SurgeryPricingBasis PricingBasis { get; init; }
    public required bool CatWasDeceased { get; init; }

    /// <summary>HO-BIL-001 Phase J (owner decision 2026-10-14): the cat's owner at discharge, the customer to bill. Null when it could not be determined.</summary>
    public Guid? OwnerAtDischargeId { get; init; }
}

public sealed class SurgeryFollowUpClosedEvent : Event
{
    public required Guid SurgeryId { get; init; }
    public required Guid FollowUpId { get; init; }
    public required FollowUpStatus Status { get; init; }
}

public sealed class AnaesthesiaEntryLoggedEvent : Event
{
    public required Guid EntryId { get; init; }
    public required Guid SurgeryId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
    public required AnaesthesiaEntryKind Kind { get; init; }
    public required bool IsControlled { get; init; }
}

public sealed class SurgicalReportFinalisedEvent : Event
{
    public required Guid SurgeryId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class SurgicalReportAddendumAddedEvent : Event
{
    public required Guid SurgeryId { get; init; }
    public required Guid AddendumId { get; init; }
}

public sealed class SurgeryCampaignChangedEvent : Event
{
    public required Guid CampaignId { get; init; }
    public required bool IsActive { get; init; }
}
