using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Surgery;

// HO-SRG-001 §12.6 -- queries never write. Staff queries are scoped to the caller's active branch (HO-ADM-001 §8.2): another branch's record is "not found".

/// <summary>Active, priced theatre services.</summary>
public sealed class GetSurgeryServicesQuery : Query<IReadOnlyList<SurgeryServiceViewModel>>
{
}

/// <summary>Surgeons qualified for the service and assistants (nurse or vet), both assigned to the branch.</summary>
public sealed class GetSurgeryStaffQuery : Query<SurgeryStaffViewModel>
{
    public required Guid BranchId { get; init; }
    public required Guid ServiceId { get; init; }
}

/// <summary><b>Never computes availability in SQL</b> -- loads through <c>ISurgerySchedulingInputs</c> and calls <c>SurgerySlotFinder</c>. With any of surgeon, assistant or theatre
/// missing, no start is offered but the busy intervals of those given are still returned.</summary>
public sealed class GetSurgeryAvailabilityQuery : Query<SurgeryAvailabilityViewModel>
{
    public required Guid BranchId { get; init; }
    public required Guid ServiceId { get; init; }
    public required DateOnly Date { get; init; }
    public Guid? SurgeonId { get; init; }
    public Guid? AssistantId { get; init; }
    public Guid? TheatreId { get; init; }
    public Guid? CatId { get; init; }
    public Guid? ExcludeSurgeryId { get; init; }
}

/// <summary>US-SRG-02 AC2 -- the theatre day list, by theatre then start, outstanding checklist items named.</summary>
public sealed class GetSurgeryDayListQuery : Query<SurgeryDayListViewModel>
{
    public required Guid BranchId { get; init; }
    public required DateOnly Date { get; init; }
}

public sealed class GetSurgeryListQuery : Query<PagedResult<SurgerySummaryViewModel>>
{
    public required Guid BranchId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public string? Status { get; init; }
    public Guid? SurgeonId { get; init; }
    public string? PricingBasis { get; init; }
    public bool? ReportOutstanding { get; init; }
}

public sealed class GetSurgeryDetailQuery : Query<SurgeryDetailViewModel?>
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
}
