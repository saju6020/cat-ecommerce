using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Prescription;

// HO-PHM-001 §12.6. A query never writes. The register is per branch; an entry of another branch is "not found".

public sealed class GetControlledDrugRegisterQuery : Query<PagedResult<RegisterEntryViewModel>>
{
    public required Guid BranchId { get; init; }
    public Guid? MedicineId { get; init; }
    public Guid? BatchId { get; init; }
    public string? Kind { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class GetControlledDrugEntryQuery : Query<RegisterEntryDetailViewModel?>
{
    public required Guid EntryId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class GetControlledDrugBalancesQuery : Query<IReadOnlyList<RegisterBalanceViewModel>>
{
    public required Guid BranchId { get; init; }
}

/// <summary>Recomputes the branch's hash chain from the entries (never trusting the stored seals) and lists entries unsealed for too long.</summary>
public sealed class VerifyControlledDrugRegisterQuery : Query<RegisterVerificationViewModel>
{
    public required Guid BranchId { get; init; }
}

public sealed class ExportControlledDrugRegisterQuery : Query<IReadOnlyList<RegisterExportRow>>
{
    public required Guid BranchId { get; init; }
    public Guid? MedicineId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    /// <summary>The most rows to read; the caller passes the export cap plus one to learn whether it was exceeded.</summary>
    public required int MaxRows { get; init; }
}

/// <summary>The branches that hold any register entry -- the worker seals each (and only those).</summary>
public sealed class GetBranchesWithUnsealedRegisterEntriesQuery : Query<IReadOnlyList<Guid>>
{
}
