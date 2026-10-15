using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Boarding;

// HO-BRD-001 section 12.6 -- the worker's candidate lists. Each returns ids only; the worker dispatches ONE command per id through the dispatcher.

/// <summary>Reserved stays whose deposit Billing shows accepted in full or awaiting a second approver (or that need none): the worker dispatches Confirm.</summary>
public sealed class GetReservationsReadyToConfirmQuery : Query<IReadOnlyList<Guid>>
{
    public required DateTimeOffset Now { get; init; }
}

/// <summary>Reserved stays past <c>DepositDueBy</c>: the worker dispatches Lapse (the handler re-reads the ledger).</summary>
public sealed class GetExpiredReservationsQuery : Query<IReadOnlyList<Guid>>
{
    public required DateTimeOffset Now { get; init; }
}

/// <summary>Enquiries whose arrival date has passed.</summary>
public sealed class GetPastEnquiriesQuery : Query<IReadOnlyList<Guid>>
{
    public required DateTimeOffset Now { get; init; }
}
