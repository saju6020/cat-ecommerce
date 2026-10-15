using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Boarding;

// HO-BRD-001 Phase G reads.

/// <summary>The stay summary (US-BRD-17 AC1): never stored, always a read. Exactly one of <c>CustomerId</c> (the owner, own stay only) and <c>BranchId</c> (staff) is given; anything else is <c>null</c> (404).</summary>
public sealed class GetStaySummaryQuery : Query<StayReportViewModel?>
{
    public required Guid StayId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? BranchId { get; init; }
}

/// <summary>The cats' vet spending headroom on a stay (US-BRD-14 AC2) -- staff.</summary>
public sealed class GetVetHeadroomQuery : Query<IReadOnlyList<CatHeadroomViewModel>?>
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>The units a stay could move to for the remaining stay through a new departure (US-BRD-15 AC2) -- staff.</summary>
public sealed class GetRelocationOptionsQuery : Query<IReadOnlyList<RelocationOptionViewModel>?>
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public DateOnly? NewDepartureOn { get; init; }
}

/// <summary>Worker: stays with uninvoiced boarding charges at a cycle boundary (cycle 1, 2, ...) and checked-out stays with charges left (cycle 0 = the final invoice).</summary>
public sealed class GetStaysDueForInvoicingQuery : Query<IReadOnlyList<StayInvoiceDueViewModel>>
{
    public required DateTimeOffset Now { get; init; }
}

/// <summary>Owner-authorised 2026-10-14 ("Show before"): what checking the stay out NOW would do, changing nothing. Staff.</summary>
public sealed class GetEarlyCheckoutPreviewQuery : Query<EarlyCheckoutPreviewViewModel?>
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
}
