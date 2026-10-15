using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Boarding;

// HO-BRD-001 section 11.5. Ids, instants, dates, enums, amounts and booleans only -- no care notes, health text, descriptions or names (U-X1).

public sealed class BoardingEnquiryCreatedEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required DateOnly ArrivalOn { get; init; }
    public required DateOnly DepartureOn { get; init; }
}

public sealed class BoardingEnquiryChangedEvent : Event
{
    public required Guid StayId { get; init; }
    public required DateOnly ArrivalOn { get; init; }
    public required DateOnly DepartureOn { get; init; }
}

public sealed class StayCareProfileSetEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required BoardingStayStatus Status { get; init; }
}

/// <summary>Staff changed a reserved stay vet authorisation or spending limit. Amounts and booleans only; the reason is read back from the stay cat.</summary>
public sealed class StayVetAuthorityChangedEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required bool BeforeAuthorised { get; init; }
    public required decimal BeforeLimit { get; init; }
    public required bool AfterAuthorised { get; init; }
    public required decimal AfterLimit { get; init; }
    public required bool ByStaff { get; init; }
}

/// <summary>The reason lives on the stay cat; <c>AuditVaccinationOverrideHandler</c> reads it back (the HO-ADM-001 section 11.6 pattern).</summary>
public sealed class VaccinationGateOverriddenEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
}

public sealed class BoardingStayReservedEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid UnitId { get; init; }
    public required DateOnly ArrivalOn { get; init; }
    public required DateOnly DepartureOn { get; init; }
    public required decimal DepositRequired { get; init; }
}

public sealed class BoardingStayConfirmedEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required DateOnly ArrivalOn { get; init; }
}

public sealed class BoardingReservationLapsedEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
}

public sealed class BoardingStayCancelledEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required bool DepositForfeited { get; init; }
}

public sealed class BoardingStayNoShowRecordedEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
}

public sealed class BoardingStayCheckedInEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
}

/// <summary>Carries the weight so the cat's weight history can record it (HO-CAT-001); no other health data.</summary>
public sealed class StayCatCheckedInEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required decimal WeightKg { get; init; }
    public required DateOnly RecordedOn { get; init; }
}

public sealed class StayCatCheckedOutEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required decimal WeightKg { get; init; }
    public required DateOnly RecordedOn { get; init; }
}

public sealed class BoardingStayRelocatedEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid UnitId { get; init; }
    public required DateTimeOffset FromInstant { get; init; }
}

public sealed class SuspectedInfectiousFlaggedEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
}

public sealed class SuspectedInfectiousClearedEvent : Event
{
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
}

public sealed class CareTaskCompletedEvent : Event
{
    public required Guid TaskId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required CareTaskKind Kind { get; init; }
}

public sealed class CareObservationRecordedEvent : Event
{
    public required Guid ObservationId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required PortionEaten Portion { get; init; }
}

public sealed class IncidentRecordedEvent : Event
{
    public required Guid IncidentId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required IncidentSeverity Severity { get; init; }
}

public sealed class IncidentEscalatedEvent : Event
{
    public required Guid IncidentId { get; init; }
    public required Guid StayId { get; init; }
}
