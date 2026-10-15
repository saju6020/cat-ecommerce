using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Prescription;

// HO-PHM-001 §12.6. The customer queries reach a prescription THROUGH its cat's CURRENT owner: anything not the caller's is not found (a 404 -- no hint that it exists).

/// <summary><c>scope</c> is <c>active</c> (the default) or <c>past</c>; <c>CatId</c> narrows to one of the caller's cats.</summary>
public sealed class GetCustomerPrescriptionsQuery : Query<IReadOnlyList<CustomerPrescriptionViewModel>>
{
    public required Guid CustomerId { get; init; }
    public string Scope { get; init; } = "active";
    public Guid? CatId { get; init; }
}

public sealed class GetCustomerPrescriptionDetailQuery : Query<CustomerPrescriptionDetailViewModel?>
{
    public required Guid PrescriptionId { get; init; }
    public required Guid CustomerId { get; init; }
}

/// <summary>Worker: prescriptions whose validity has passed with supplies left (a superset -- the command re-checks with the prescription's own branch date).</summary>
public sealed class GetExpirablePrescriptionsQuery : Query<IReadOnlyList<Guid>>
{
    public int Limit { get; init; } = 200;
}

/// <summary>Worker: repeat prescriptions inside their reminder window (§8.11) for <paramref name="Today"/> and <paramref name="LeadDays"/>.</summary>
public sealed class GetDueRefillRemindersQuery : Query<IReadOnlyList<DueRefillReminderViewModel>>
{
    public required DateOnly Today { get; init; }
    public required int LeadDays { get; init; }
}
