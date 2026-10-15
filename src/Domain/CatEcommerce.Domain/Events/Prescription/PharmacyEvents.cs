using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Prescription;

// HO-PHM-001 §11.5. Ids, dates, instants, enumerations, decimals and booleans only -- NO strings (no instructions, reasons, allergen text or names)
// (U-X1: reflection over this namespace). A reason or an acknowledgement note lives on the aggregate and is read back by the audit handler.
// The bold events of the handover are integration contracts for HO-BIL-001, HO-SUB-001 and HO-ORD-001: their handlers must be idempotent and must
// not enqueue (HO-NTF-001 ADR-057).

public sealed class FormularyMedicineCreatedEvent : Event
{
    public required Guid MedicineId { get; init; }
}

public sealed class FormularyMedicineUpdatedEvent : Event
{
    public required Guid MedicineId { get; init; }
}

public sealed class FormularyMedicineControlledStatusChangedEvent : Event
{
    public required Guid MedicineId { get; init; }
    public required bool IsControlled { get; init; }
}

public sealed class FormularyMedicineActivationChangedEvent : Event
{
    public required Guid MedicineId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class PrescriptionIssuedEvent : Event
{
    public required Guid PrescriptionId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid VisitId { get; init; }
    public required Guid MedicineId { get; init; }
    public required bool IsControlled { get; init; }
    public required bool HadAcknowledgedWarnings { get; init; }
}

public sealed class PrescriptionCancelledEvent : Event
{
    public required Guid PrescriptionId { get; init; }
    public required Guid CatId { get; init; }
}

public sealed class PrescriptionExpiredEvent : Event
{
    public required Guid PrescriptionId { get; init; }
    public required Guid CatId { get; init; }
}

/// <summary>Integration contract (HO-BIL-001): raised once per supply, with a price snapshot per line.</summary>
public sealed class PrescriptionDispensedEvent : Event
{
    public required Guid DispensingId { get; init; }
    public required Guid PrescriptionId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid VisitId { get; init; }
    public required int SupplyNumber { get; init; }
    public required IReadOnlyList<DispensedLineFact> Lines { get; init; }
}

/// <summary>Integration contract (HO-BIL-001): a same-period reversal of a supply.</summary>
public sealed class DispensingReversedEvent : Event
{
    public required Guid DispensingId { get; init; }
    public required Guid PrescriptionId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid CatId { get; init; }
    public required int SupplyNumber { get; init; }
}

public sealed class LabelPrintedEvent : Event
{
    public required Guid DispensingId { get; init; }
    public required Guid PrescriptionId { get; init; }
    public required int PrintCount { get; init; }
    public required bool IsControlled { get; init; }
}

public sealed class RepeatRequestedEvent : Event
{
    public required Guid PrescriptionId { get; init; }
    public required Guid RepeatRequestId { get; init; }
    public required Guid CatId { get; init; }
}

public sealed class RepeatRequestDeclinedEvent : Event
{
    public required Guid PrescriptionId { get; init; }
    public required Guid RepeatRequestId { get; init; }
    public required Guid CatId { get; init; }
}

public sealed class ControlledDrugRegisterEntryRecordedEvent : Event
{
    public required Guid EntryId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid MedicineId { get; init; }
    public required ControlledDrugEntryKind Kind { get; init; }
}

/// <summary>Integration contract (HO-ORD-001, HO-SUB-001).</summary>
public sealed class DietAuthorisationIssuedEvent : Event
{
    public required Guid AuthorisationId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid ProductId { get; init; }
    public required DateOnly ValidFrom { get; init; }
    public required DateOnly ValidTo { get; init; }
}

public sealed class DietAuthorisationExtendedEvent : Event
{
    public required Guid AuthorisationId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid ProductId { get; init; }
    public required DateOnly ValidTo { get; init; }
}

/// <summary>Integration contract (HO-SUB-001 suspends a prescription-diet subscription on this).</summary>
public sealed class DietAuthorisationExpiredEvent : Event
{
    public required Guid AuthorisationId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid ProductId { get; init; }
    public required DateOnly ValidTo { get; init; }
}

public sealed class DietAuthorisationRevokedEvent : Event
{
    public required Guid AuthorisationId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid ProductId { get; init; }
    public required DateOnly ValidTo { get; init; }
}
