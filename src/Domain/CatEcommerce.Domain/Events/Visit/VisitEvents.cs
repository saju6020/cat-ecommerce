using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Visit;

// HO-EMR-001 §11.5. Ids, instants, enums, numbers and booleans only -- no clinical text, names or
// file names (U-X1): an event reaches handlers and logs, and a clinical note must reach neither.

public sealed class VisitOpenedEvent : Event
{
    public required Guid VisitId { get; init; }
    public required Guid AppointmentId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class VisitVitalsRecordedEvent : Event
{
    public required Guid VisitId { get; init; }
    public required Guid VitalsEntryId { get; init; }
    public required bool HasWeight { get; init; }
}

public sealed class ConsultationNoteSavedEvent : Event
{
    public required Guid VisitId { get; init; }
    public required int ActiveProblemCount { get; init; }
}

/// <summary>HO-BIL-001 contract (§11.5): Billing reads the visit's charge lines when it sees this. <see cref="CustomerId"/> is the cat's owner at
/// finalising, the customer to bill (owner decision 2026-10-14); it is stored on the visit and never changes. There is no boarding-stay id until
/// HO-BRD-001 (deferred by the same decision).</summary>
public sealed class VisitFinalisedEvent : Event
{
    public required Guid VisitId { get; init; }
    public required Guid AppointmentId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid FinalisedBy { get; init; }
    public required DateTimeOffset FinalisedOn { get; init; }
}

public sealed class VisitAddendumAddedEvent : Event
{
    public required Guid AddendumId { get; init; }
    public required Guid VisitId { get; init; }
    public required Guid CatId { get; init; }
}

public sealed class ClinicalPhotoAttachedEvent : Event
{
    public required Guid VisitId { get; init; }
    public required Guid PhotoId { get; init; }
    public required Guid FileId { get; init; }
}

public sealed class FollowUpSetEvent : Event
{
    public required Guid VisitId { get; init; }
    public required FollowUpKind Kind { get; init; }
    public required DateOnly DueOn { get; init; }
}

public sealed class FollowUpClearedEvent : Event
{
    public required Guid VisitId { get; init; }
}

// Phase F -- treatments and stock (US-CLN-15).

public sealed class TreatmentRecordedEvent : Event
{
    public required Guid VisitId { get; init; }
    public required Guid LineId { get; init; }
    public required TreatmentKind Kind { get; init; }
    public required bool IsBillable { get; init; }
    public required decimal DeductedQuantity { get; init; }
    public required decimal ShortfallQuantity { get; init; }
}

public sealed class TreatmentReversedEvent : Event
{
    public required Guid VisitId { get; init; }
    public required Guid LineId { get; init; }

    /// <summary>True when stock had been deducted and a compensating movement was enlisted with the reversal.</summary>
    public required bool StockReturned { get; init; }
}

/// <summary>HO-IMP-001 contract (§11.5): a treatment was given from more than the dispensary held. The movement itself is the
/// <c>Shortfall</c> alert Inventory's <c>RecordShortfall</c> enlisted; this is the clinic's own fact, ids and numbers only.</summary>
public sealed class ClinicalStockShortfallRecordedEvent : Event
{
    public required Guid VisitId { get; init; }
    public required Guid LineId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid SkuId { get; init; }
    public required decimal ShortfallQuantity { get; init; }
    public required decimal DeductedQuantity { get; init; }
}
