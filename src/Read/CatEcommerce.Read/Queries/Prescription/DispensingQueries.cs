using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Prescription;

// HO-PHM-001 §12.6. A query never writes; the dispense plan asks Inventory and the pure DispensingBatchPlanner and writes nothing.

public sealed class GetPharmacyQueueQuery : Query<PharmacyQueueViewModel>
{
    public required Guid BranchId { get; init; }
}

/// <summary>The scanned or typed <c>RX-…</c> number; <c>null</c> when none matches.</summary>
public sealed class LookupPrescriptionByNumberQuery : Query<PrescriptionLookupViewModel?>
{
    public required string Number { get; init; }
}

public sealed class GetDispensePlanQuery : Query<DispensePlanViewModel?>
{
    public required Guid PrescriptionId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class GetDispensingQuery : Query<DispensingViewModel?>
{
    public required Guid DispensingId { get; init; }
    public required Guid BranchId { get; init; }
    public required IReadOnlyList<string> CallerRoles { get; init; }
}
