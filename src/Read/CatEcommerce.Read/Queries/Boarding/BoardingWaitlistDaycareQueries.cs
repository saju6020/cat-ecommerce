using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Boarding;

// HO-BRD-001 Phase H reads (section 12.6).

public sealed class GetCustomerWaitlistQuery : Query<IReadOnlyList<BoardingWaitlistEntryViewModel>>
{
    public required Guid CustomerId { get; init; }
}

/// <summary>Staff: the daycare of one day at the active branch -- capacity, slots taken and the visits.</summary>
public sealed class GetDaycareDayQuery : Query<DaycareDayViewModel>
{
    public required Guid BranchId { get; init; }
    public required DateOnly Date { get; init; }
}

/// <summary>The owner's own daycare visits, newest first.</summary>
public sealed class GetCustomerDaycareQuery : Query<IReadOnlyList<DaycareVisitViewModel>>
{
    public required Guid CustomerId { get; init; }
}

public sealed class GetDaycareRateCardsQuery : Query<IReadOnlyList<DaycareRateCardViewModel>>
{
    public required Guid BranchId { get; init; }
}

/// <summary>Worker: waiting entries, oldest first.</summary>
public sealed class GetWaitlistCandidatesQuery : Query<IReadOnlyList<Guid>>
{
    public required DateTimeOffset Now { get; init; }
}

/// <summary>Worker: waiting entries whose arrival date may have passed (the handler decides in the branch's zone).</summary>
public sealed class GetStaleWaitlistEntriesQuery : Query<IReadOnlyList<Guid>>
{
    public required DateTimeOffset Now { get; init; }
}

/// <summary>Worker: checked-in stays whose branch-local time has reached <c>boarding.dailyUpdateHour</c>, with the local date to send for.</summary>
public sealed class GetDailyUpdateCandidatesQuery : Query<IReadOnlyList<DailyUpdateCandidateViewModel>>
{
    public required DateTimeOffset Now { get; init; }
}
