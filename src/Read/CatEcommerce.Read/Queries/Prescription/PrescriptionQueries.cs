using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Prescription;

// HO-PHM-001 §12.6. A query never writes -- the safety check in particular is a query and writes nothing.

/// <summary>The server's safety assessment of a prescription that has not been issued (US-PHM-02, US-PHM-03). The body is the C5 fields without the
/// acknowledgements. <c>null</c> when the cat or the medicine does not exist.</summary>
public sealed class CheckPrescriptionSafetyQuery : Query<SafetyAssessmentViewModel?>
{
    public required Guid CatId { get; init; }
    public required Guid MedicineId { get; init; }
    public required decimal DoseAmount { get; init; }
    public required string DoseUnit { get; init; }
    public required string Route { get; init; }
    public required string Frequency { get; init; }

    /// <summary>The active branch -- "today" and the weight window are branch-local.</summary>
    public required Guid BranchId { get; init; }
}

/// <summary><c>scope</c> is <c>active</c> (the default) or <c>all</c>.</summary>
public sealed class GetCatPrescriptionsQuery : Query<PagedResult<PrescriptionSummaryViewModel>>
{
    public required Guid CatId { get; init; }
    public string Scope { get; init; } = "active";
    public required Guid BranchId { get; init; }
}

public sealed class GetPrescriptionDetailQuery : Query<PrescriptionDetailViewModel?>
{
    public required Guid PrescriptionId { get; init; }
    public required IReadOnlyList<string> CallerRoles { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>HO-EMR-001's contract (discharge summary, consultation history): the prescriptions written at a visit.</summary>
public sealed class GetVisitPrescriptionsQuery : Query<IReadOnlyList<PrescriptionSummaryViewModel>>
{
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>HO-EMR-001's contract (clinical history, medical-history PDF): every prescription ever written for the cat, newest first.</summary>
public sealed class GetCatPrescriptionHistoryQuery : Query<IReadOnlyList<PrescriptionSummaryViewModel>>
{
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
}
